using System;
using System.Diagnostics;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Content;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.GameAdapter.OnlineUi;
using CasualtiesUnknownOnline.GameAdapter.Patches;
using CasualtiesUnknownOnline.GameAdapter.World;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Diagnostics;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.Tutorial;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Runtime.Time;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UnityEngine;
using IGameAdapter = CasualtiesUnknownOnline.Runtime.GameAdapter.IGameAdapter;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Game Adapter for the current Casualties Unknown (Demo) build (architecture.md
/// §4). The only layer that knows game types. Thin coordinator: owns the
/// lifecycle (probe/install/uninstall), the Update pump (orchestrating the
/// domain pumps) and the Runtime boundary interfaces. The Harmony patch bridge,
/// session wiring and direct player-interaction apply side live in real
/// top-level collaborators, and the deep domain logic lives in the modules
/// composed by <see cref="GameAdapterDomains"/>.
/// </summary>
public sealed class GameAdapter : IGameAdapter, IOnlineUiNativeFactsQuery, IOnlineUiSurface, ICuoService, IModEntitySpawner, IModItemSpawner, IModTilePlacer, IModStructurePlacer, IModLiquidPlacer, IModNativeApiProvider, ILayerPanelVisibilityPump
{
	// The intro cover's decision, static because the patch that reads it
	// (PreRunScriptIntroSkipPatch) runs before any adapter instance is involved: the shell
	// states the player's preference and the join intent through IJoinFlowPresentation, and
	// this object owns the rule (IntroCoverDecision weighs the two facts).
	private static readonly IntroCoverDecision IntroCoverDecision = new();

	/// <summary>Read by <c>PreRunScriptIntroSkipPatch</c>: the named decision, never a bare flag.</summary>
	internal static IntroCoverDecision IntroCover => IntroCoverDecision;

	/// <summary>Where the intro decision's one-line reason goes; the patch has no logger of its own.</summary>
	private static Action<string>? _introCoverLog;

	/// <summary>Write the intro decision's reason once, at the edge that reads it (a low-frequency event).</summary>
	internal static void LogIntroCover(string reason) => _introCoverLog?.Invoke(reason);

	private readonly GameAdapterDomains _domains;
	private readonly GameAdapterBridge _bridge;
	private readonly PlayerInteractionApply _playerInteraction;
	private readonly RemoteIntentApplier _remoteInventoryApply;
	private readonly GameAdapterSessionBinding _sessionBinding;
	private readonly LatencyInstrumentation _latency;
	private readonly PatchInstallLifecycle _patches;
	// The Online UI's read-only probe of the game's own UI (ticket online-ui-art-and-controls-overhaul,
	// S1): it owns the CUO canvas parented under the game's main canvas and the game's settings rows.
	private readonly OnlineUiNativeFactsCapture _onlineUiNativeFacts = new();
	// The Online UI's LIVE surface (ticket online-ui-art-and-controls-overhaul, S2a): CUO's canvas
	// parented under the game's canvas with the game's own launcher control on it. The probe above READS
	// the game's UI; this one IS the UI — it renders, takes input and owns the game objects.
	private readonly OnlineUiSurfaceHost _onlineUiSurface;
	private Body? _lastLocalBody; // Unity object — == (the world-entry edge for the destroy-suppression reset)

	public GameAdapter(
		ISessionControl session,
		AdaptiveStreamRateService adaptiveRates,
		IEntitySyncControl entities,
		ICharacterDataControl characterData,
		IWorldControl world,
		IWorldFactSource worldFacts,
		NativeWorldFacts nativeWorldFacts,
		IItemControl items,
		ICraftControl craft,
		ItemArbitration arbitration,
		EnemySyncService enemies,
		IWorldTimeControl worldTime,
		ILayerAdvanceControl layerAdvance,
		IReentryControl reentry,
		IPlayerInteractionControl playerInteraction,
		ITutorialClawControl tutorialClaw,
		IWorldSaveControl worldSaves,
		IOptionsMonitor<RespawnOptions> respawnOptions,
		IHostRules hostRules,
		AnnouncedHostRules announced,
		RemoteVitalsService remoteVitals,
		ILocalizationService localization,
		WorldEntityKernelProjection worldEntityKernel,
		WorldEntryFanout worldBackfill,
		ILogger<GameAdapter> log,
		LatencyInstrumentation latency,
		IMapper mapper,
		ILimbUseSemantics limbUseSemantics,
		IConsumeSemantics consumeSemantics,
		IWearSemantics wearSemantics,
		ILoggerFactory loggerFactory,
		GameAdapterItemContentProvider itemContent,
		GameAdapterBuildingContentProvider buildingContent,
		GameAdapterTileContentProvider tileContent,
		GameAdapterLiquidTileContentProvider liquidTileContent,
		GameAdapterStructureContentProvider structureContent,
		GameAdapterStatusContentProvider statusContent,
		GameAdapterMoodleContentProvider moodleContent,
		ModStatusStore modStatusStore,
		ModStatusProjectionReadModel modStatusProjectionReadModel,
		WorldRestoreAudit restoreAudit,
		IStartingSupplyPublisher startingSupplies,
		ISolidFoodSemantics solidFoodSemantics,
		IShrapnelGripSlipRandom gripSlipRandom,
		ITimeSource time,
		ItemIdAllocator itemIds,
		IRemotePlacementResolver placementResolver)
	{
		_patches = new PatchInstallLifecycle(log);
		_onlineUiSurface = new OnlineUiSurfaceHost(log);
		_latency = latency;
		_introCoverLog = reason => log.LogInformation("[Intro] {Reason}", reason);
		_domains = new GameAdapterDomains(session, adaptiveRates, entities, characterData, world, worldFacts, nativeWorldFacts, items, craft, arbitration,
			enemies, worldTime, layerAdvance, reentry, playerInteraction, tutorialClaw, worldSaves, restoreAudit, startingSupplies, respawnOptions, hostRules,
			announced, remoteVitals, localization, worldEntityKernel, worldBackfill, log, mapper, limbUseSemantics, consumeSemantics, wearSemantics, solidFoodSemantics, gripSlipRandom, loggerFactory, itemContent, buildingContent, tileContent, liquidTileContent, structureContent, statusContent, moodleContent, modStatusStore, modStatusProjectionReadModel, time, itemIds);
		// Composition seam: the adapter owns the DEFERRED creation reports (a drop's
		// velocity, a destructive trap's building-death drops, a block break's drops)
		// and the item domain settles them before it reports an operation, so the host
		// always judges an item's creation before any operation on it (no hold window).
		items.RegisterPendingCreationSource(new PendingItemCreationReports(_domains.ItemWorldSync, _domains.EntityEventSync, _domains.BlockBreakSync));
		_bridge = new GameAdapterBridge(_domains);
		_playerInteraction = new PlayerInteractionApply(_domains, placementResolver);
		_remoteInventoryApply = new RemoteIntentApplier(_domains);
		var pushApply = new PlayerPushApply(_domains);
		var medicalOperationApply = new MedicalOperationApply(_domains);

		_sessionBinding = new GameAdapterSessionBinding(_domains, _playerInteraction, _remoteInventoryApply, pushApply, medicalOperationApply);
		PatchBridge.Bind(_bridge); // the only static seam — Harmony patches read the narrow surface, never this instance
	}

	public string CapabilityReport { get; private set; } = "Not probed";

	/// <summary>
	/// The console <c>/heal</c> cheat verb's body half: the full reset of THIS client's own body,
	/// with its severed-limb restore, hollow clear and re-report. The host instructs (with the
	/// instruction id the applied event echoes), this client applies — the body is its owner's.
	/// </summary>
	LocalBodyResetOutcome ILocalBodyReset.Reset(ulong? instructionId) => _domains.LocalBodyReset.Reset(instructionId);

	/// <summary>
	/// The startup gate <c>ICuoService.Initialize</c> drives: probe the loaded
	/// game assembly (the four types and the report text live in the patch life
	/// cycle, declared as the session capability's game types), then install only
	/// when the probe passes. Not a consumer port — nothing outside this class
	/// installs or uninstalls patches, so the capability stays internal to the
	/// adapter instead of being forced on every implementation.
	/// </summary>
	private bool ProbeGame()
	{
		var ok = PatchInstallLifecycle.ProbeGame(out var report);
		CapabilityReport = report;
		return ok;
	}

	private bool Install() => _patches.Install();

	private void Uninstall() => _patches.Uninstall();

	void ICuoService.Initialize()
	{
		CharacterDataMapper.Configure();
		_sessionBinding.Bind();
		if (ProbeGame())
		{
			Install();
		}
		else
		{
			_domains.Log.LogError("Game Adapter probe failed — CUO multiplayer unavailable.");
		}

		// Stage 1's probe output: ONE report naming every capability with its
		// class, its contract count and its failure reason (see the reporter).
		_patches.Publish(CapabilityReport);
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update()
	{
		var frameStopwatch = _latency.IsEnabled ? Stopwatch.StartNew() : null;
		_domains.GuestMenu.Update();
		_domains.RunSettingsRange.Update();
		_domains.MenuInput.Update();
		using (_latency.Measure("Run"))
		{
			_domains.Run.Update();
		}
		_domains.Reentry.Update(); // guest: the bounded ask for the entry instruction it may have missed; host: the answer rides the event
		_domains.PlayerInteraction.Update(); // host: a placement question nobody answered is refused at its deadline, not at the next give

		using (_latency.Measure("WorldTime"))
		{
			_domains.WorldTimeSync.Update(); // host policy + direct-write adoption + resend; guest ramp stepping and host-speed enforcement (suspended while a local initiation is in flight)
		}

		// World-entry edge: the teardown of the PREVIOUS scene finished (its
		// destroys were suppressed, #191) — the new world's real destroys report
		// again. The edge rides the local body (null in any menu scene; Unity
		// object — ==).
		var localBody = _domains.Run.LocalBody;
		if (localBody != null && _lastLocalBody == null) // Unity objects — ==
		{
			_domains.ItemWorldSync.ResetDestroySuppression();
		}

		// The custody projection (shadow mode): the kernel's rows against this client's objects, once a second, changing nothing. Stage 1 of
		// docs/backlog/todo/item-custody-single-authority.md — a running session with `mismatches=0` is what lets the readers be switched.
		_domains.ItemCustody.Tick(_domains.Session.LocalSteamId);

		_lastLocalBody = localBody;
		using (_latency.Measure("StartGate"))
		{
			_domains.Gate.Update(_domains.Run.LocalBody);
		}

		_domains.LayerAdvance.UpdatePanelChoices(); // the end-of-layer panel's two choices belong to the host
		_domains.GenItemAuthority.Update(); // host/solo: publish the generation-time items when the generation finished
		_domains.GenItemApplication.Update(_domains.Run.IsEnteringWorld); // guest: apply the host's generation snapshot once the local generation finished (held while the entry loads its world)
		_domains.StartingSupplies.Update(); // a player this world has no character for: the run's starting supplies, once per body (S4.3)
		using (_latency.Measure("Respawn"))
		{
			_domains.Respawn.Update(); // host: next-level respawn once a world generation finishes
		}
		_domains.TrapLayoutScanner.Update(); // host: report the generated trap layout on the same falling edge
		_domains.TrapLayoutApplication.Update(); // guest: apply a deferred layout snapshot once the local generation finished
		_domains.LayerModifierSync.Update(); // guest: apply the host's layer modifier once the local generation finished
		_domains.CarriedInventoryReporter.Update(); // both sides: register/report the carried inventory with instance ids once the local generation finished
		_domains.ItemWorldSync.FlushPendingDrop(); // a drop that was not thrown reports at end of frame (one drop = one report)
		_domains.EntityEventSync.FlushPendingDrops(); // a destructive trap's building-death drops fold into the same event after the hold window
		_domains.BlockBreakSync.FlushPendingBlockBreak(); // a break's drops fold in one frame after the break — the break + drops go out as ONE message
		using (_latency.Measure("ItemPosition"))
		{
			if (_domains.Session.Role == SessionRole.Host && _domains.Session.SessionActive)
			{
				_domains.ItemPositionAuthority.Update(); // the host's physics is the single position authority
			}
			else
			{
				_domains.ItemPositionFollow.Update(); // the guest copies simulate locally (ground-layer isolation), soft-corrected by the host's stream
			}
		}

		using (_latency.Measure("WorldEvent"))
		{
			_domains.WorldEventSync.Update();
		}
		_domains.GeyserStateSync.Update(); // host/solo: capture + broadcast the geysers' liquid types once the generation finished
		_domains.RadiationLineSync.Update(); // host: publish the authoritative radiation-line state (active + timeGone)
		_domains.Spectator.Update(); // every client: a dead player's own camera, from this client's own facts
		_domains.EntitySpawnSync.Update(); // the creation channel's deferred reports (a geyser's type, after its child Start) and carried-data applications
		using (_latency.Measure("Fluid"))
		{
			_domains.FluidSync.Update(); // host: stream the members' fluid viewports (10 Hz diff + 1 Hz full)
		}

		using (_latency.Measure("Trader"))
		{
			_domains.TradeSync.Update(); // host: the 5 s trader-state fallback broadcast
		}
		_domains.BlockBreakSync.Update(); // expire break records without a consuming drops report
		using (_latency.Measure("Renderer"))
		{
			_domains.Renderer.Update(localBody);
		}

		// A carried local body follows the remote carrier's RENDER clone. The
		// renderer must run first so the carrier clone has already received this
		// frame's SessionStatePump interpolation; reading the raw entity buffer
		// there would reintroduce the step/snap the render path intentionally
		// smooths away.
		_playerInteraction.UpdateCarriedBody(localBody);

		_domains.CharacterRagdollSync.Update(); // flush clone-creation-race ragdoll one-shots after the renderer created clones
		_domains.RemoteBackpack.Update();
		_domains.RemoteMedical.Update();
		using (_latency.Measure("EnemySync"))
		{
			_domains.EnemySync.Update(); // host: capture + publish the simulated enemies; guest: (event-driven bind/apply)
		}

		using (_latency.Measure("EnemyCombat"))
		{
			_domains.EnemyCombat.Update(); // host: enemy combat decisions (target guidance rides the patch callbacks; bite arbitration here)
		}
		_domains.TutorialClawSync.Update(); // host: publish the tutorial-claw presentation state (Runtime throttles the 20 Hz fan-out)

		// THE CUT SEAM — the last step of the frame pump. Every domain above has
		// finished this frame's work (the drop/break flushes included), so the
		// kernel is at a committed revision and no frame flush is in flight: the
		// one point where the armed /save cut and the host's deliberate menu return
		// are taken. Nothing else in the adapter may call the save control.
		using (_latency.Measure("SaveSeam"))
		{
			_domains.SaveCutSeam.Update(_domains.Run.IsInWorld);
		}

		if (frameStopwatch != null)
		{
			_latency.RecordFrame(frameStopwatch.Elapsed.TotalMilliseconds);
		}

		_latency.Flush();

		// The call-identity guard's frame boundary (CallContext.EndFrame): a scope still open here was leaked
		// by a frame that threw between its Prefix and its Postfix, and one line per leak episode is what an
		// operator reads — never one per frame.
		var recovery = CallContext.EndFrame();
		if (recovery is not null)
		{
			_domains.Log.LogError(
				"[CallContext] frame boundary: {Recovery} — a game-side throw inside a patched original skips the postfix that owns the release.",
				recovery);
		}
	}

	/// <summary>
	/// Late-frame carry presentation pass (the <see cref="ICarryPresentationPump"/>
	/// port). The host shell calls it from LateUpdate after every game/cuo Update has
	/// run. This is the final carrier-side pin: every remote rider clone must sit on
	/// the local carrier's final body transform for the frame that is about to
	/// render, even if a game script (or the native body simulation) moved the carrier
	/// after <see cref="RemotePlayerRenderer.Update"/> or after
	/// <see cref="BodyUpdatePatch"/> re-pinned it.
	/// </summary>
	void ICarryPresentationPump.PinCarriedPresentation()
	{
		var localBody = _domains.Run.LocalBody;
		if (localBody == null) // Unity object — ==
		{
			return;
		}

		_domains.Renderer.RefreshLocalCarrierAttach(localBody);
	}

	/// <summary>
	/// The end-of-layer panel's late-frame write (<see cref="ILayerPanelVisibilityPump"/>): the host rule decides by the
	/// LIVING group's share, and the game's own <c>WorldGeneration.Update</c> writes the same object every frame — so
	/// this belongs to the last phase, where nothing overwrites it before the frame renders.
	/// </summary>
	void ILayerPanelVisibilityPump.PumpLayerPanelVisibility() => _domains.LayerPanel.Update();

	void ICuoService.Stop() => Uninstall();

	void IDisposable.Dispose()
	{
		_onlineUiNativeFacts.Dispose();
		_onlineUiSurface.Dispose();
		_domains.WorldTimeSync.Unbind();
		_sessionBinding.Unbind();
		_domains.Renderer.DestroyAllClones();
		_domains.StandingMaterializer.ResetSessionState(); // the carried rows' incarnations are scene objects this side owns — teardown takes them like the clones
		PatchBridge.Unbind(_bridge);
	}

	// ---- capability ports (the aggregate declares no member of its own) ----

	bool IDragUseCueState.Showing => _domains.DragUseCue.Showing;

	ulong IDragUseCueState.TargetSteamId => _domains.DragUseCue.TargetSteamId;

	bool IStartGateState.IsWaitingForReady => _domains.Gate.WaitingForReady;

	string IStartGateState.WaitingText => _domains.Gate.WaitingText;

	bool IStartGateState.WaitingTextIsOnTheNativeScreen => _domains.Gate.WaitingTextIsOnTheNativeScreen;

	bool IWorldPresenceQuery.IsInWorldOrGenerating => _domains.Run.IsInWorldOrGenerating;

	void IGameIntegrationLifecycle.OnApplicationQuit() => _domains.ItemWorldSync.SuppressDestroys();

	bool ILocalHealItemQuery.HasLocalHealItem() => _playerInteraction.HasLocalHealItem();

	IReadOnlyList<LocalHealItem> ILocalHealItemQuery.GetLocalHealItems() => _playerInteraction.GetLocalHealItems();

	bool ITraderRecruitRequest.TryRequestTraderRecruit(ulong targetSteamId) => _domains.TraderRecruit.TryRequest(targetSteamId);

	void INativeInputBlocker.SetOnlineUiModal(bool visible) => _domains.MenuInput.SetModal(visible);

	bool INativeInputBlocker.IsTheGamesOwnTextInputFocused => OnlineUiKeyboardFacts.IsTheGameUsingTheKeyboard();

	bool ISpectatorControl.IsWatching => _domains.Spectator.IsWatching;

	bool ISpectatorControl.IsSpectator => _domains.Spectator.IsSpectator;

	bool ISpectatorControl.IsFreeFly => _domains.Spectator.IsFreeFly;

	bool ISpectatorControl.MirrorEffects
	{
		get => _domains.Spectator.MirrorEffects;
		set => _domains.Spectator.MirrorEffects = value;
	}

	bool ISpectatorControl.EnterSpectatorMode() => _domains.Spectator.EnterSpectatorMode();

	void ISpectatorControl.ExitSpectatorMode() => _domains.Spectator.ExitSpectatorMode();

	bool ISpectatorControl.TryWatch(ulong steamId) => _domains.Spectator.TryWatch(steamId);

	ulong ISpectatorControl.WatchingSteamId => _domains.Spectator.WatchingSteamId;

	void ISpectatorControl.StopWatching() => _domains.Spectator.StopWatching();

	bool ISpectatorControl.TrySetFreeFly(bool wanted) => _domains.Spectator.TrySetFreeFly(wanted);

	void INativeInputBlocker.SetOnlineUiEscapeSurfaceVisible(bool visible) =>
		_domains.MenuInput.SetNonModalEscapeSurfaceVisible(visible);

	bool IRemoteInventoryPresentation.OpenRemoteBackpack(ulong targetSteamId, string displayName) =>
		_domains.RemoteBackpack.Open(targetSteamId, displayName);

	bool IRemoteMedicalPresentation.OpenRemoteMedical(ulong targetSteamId, string displayName) =>
		_domains.RemoteMedical.Open(targetSteamId, displayName);

	bool IPlayerAnchorQuery.TryGetRemoteHeadPosition(ulong steamId, out float x, out float y)
	{
		if (_domains.Renderer.TryGetRemoteHeadPosition(steamId, out var head))
		{
			x = head.x;
			y = head.y;
			return true;
		}

		x = 0f;
		y = 0f;
		return false;
	}

	void IJoinFlowPresentation.PrepareForDirectJoin() => IntroCoverDecision.NoteDirectJoinLaunch();

	void IJoinFlowPresentation.ConfigureIntroCoverPreference(Func<bool> skipsTheCover) => IntroCoverDecision.UsePreference(skipsTheCover);

	/// <summary>
	/// The Online UI's read-only probe of the game's own UI (ticket online-ui-art-and-controls-overhaul,
	/// S1). Only the adapter can reach the game's canvas and prefabs, so the later stages read the font,
	/// the row styles, the chrome styles and the canvas scale from this one call.
	/// </summary>
	OnlineUiNativeFacts IOnlineUiNativeFactsQuery.Capture() => _onlineUiNativeFacts.Capture();

	/// <summary>
	/// The Online UI's live surface (ticket online-ui-art-and-controls-overhaul, S2a). The Runtime pushes
	/// what to show; the surface puts it on the game's own controls and reports the player's clicks and
	/// hover back. Nothing about the game's objects crosses this seam in either direction.
	/// </summary>
	void IOnlineUiSurface.Push(OnlineUiFrame frame) => _onlineUiSurface.Push(frame);

	bool IOnlineUiSurface.TryDequeueIntent(out OnlineUiIntent intent) => _onlineUiSurface.TryDequeueIntent(out intent);

	// ---- Mod runtime boundaries (Phase 4 Mod API) ----

	bool IModEntitySpawner.TrySpawnEntity(string prefabId, float x, float y, float rotation) =>
		_domains.EntitySpawnSync.TrySpawnFromMod(prefabId, x, y, rotation);

	bool IModItemSpawner.TrySpawnItem(string itemId, float x, float y, float rotation)
	{
		if (!_domains.Session.SessionActive || string.IsNullOrWhiteSpace(itemId))
		{
			return false;
		}

		var createdGo = Utils.Create(itemId, new Vector2(x, y), 0f);
		if (createdGo == null) // Unity object — ==
		{
			return false;
		}

		var created = createdGo.GetComponent<Item>();
		if (created == null) // Unity object — ==
		{
			_domains.Log.LogWarning("[ItemSpawn] mod-requested id {Id} has no Item — the local copy is destroyed.", itemId);
			Object.Destroy(createdGo);
			return false;
		}

		created.transform.eulerAngles = new Vector3(0f, 0f, rotation);
		_domains.Log.LogInformation("[ItemSpawn] mod-requested {Id} created at ({X:F1},{Y:F1}); the Item.Start report will replicate it.", itemId, x, y);
		return true;
	}

	bool IModTilePlacer.TryPlaceBlock(string tileId, int x, int y)
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			_domains.Log.LogWarning("[TilePlacement] mod-requested tile {Id} was refused because no world is active.", tileId);
			return false;
		}

		if (!_domains.TileContent.TryPrepareForPlacement(tileId, world, out var index))
		{
			_domains.Log.LogWarning("[TilePlacement] mod-requested tile {Id} is not a bound custom tile in the current world — refused.", tileId);
			return false;
		}

		var blockPos = new Vector2Int(x, y);
		if (world.GetBlock(blockPos) != 0)
		{
			_domains.Log.LogWarning("[TilePlacement] mod-requested tile {Id} at block ({X},{Y}) is not air — refused.", tileId, x, y);
			return false;
		}

		world.SetBlock(blockPos, index);
		_domains.Log.LogInformation("[TilePlacement] mod-requested tile {Id} placed at block ({X},{Y}); the SetBlock relay will replicate it.", tileId, x, y);
		return true;
	}

	bool IModStructurePlacer.TryPlaceStructure(string structureId, int originX, int originY)
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			_domains.Log.LogWarning("[StructurePlacement] mod-requested structure {Id} was refused because no world is active.", structureId);
			return false;
		}

		if (!_domains.StructureContent.TryGetCompiled(structureId, out var structure))
		{
			_domains.Log.LogWarning("[StructurePlacement] mod-requested structure {Id} is not a bound custom structure — refused.", structureId);
			return false;
		}

		var worldWidth = (int)world.width;
		var worldHeight = (int)world.height;
		var writes = new List<(Vector2Int Pos, ushort Block)>();
		foreach (var cell in structure.Cells)
		{
			var x = originX + cell.X;
			var y = originY + cell.Y;
			if (x < 0 || y < 0 || x >= worldWidth || y >= worldHeight)
			{
				_domains.Log.LogWarning(
					"[StructurePlacement] mod-requested structure {Id} at ({X},{Y}) has cell offset ({OffsetX},{OffsetY}) outside the world — refused.",
					structureId, originX, originY, cell.X, cell.Y);
				return false;
			}

			var blockPos = new Vector2Int(x, y);
			if (world.GetBlock(blockPos) != 0)
			{
				_domains.Log.LogWarning(
					"[StructurePlacement] mod-requested structure {Id} at ({X},{Y}) cell ({CellX},{CellY}) is not air — refused.",
					structureId, originX, originY, x, y);
				return false;
			}

			if (cell.IsCustomTile)
			{
				if (!_domains.TileContent.TryPrepareForPlacement(cell.TileId!, world, out var customIndex))
				{
					_domains.Log.LogWarning(
						"[StructurePlacement] mod-requested structure {Id} references custom tile {TileId}, which is not available in the current world — refused.",
						structureId, cell.TileId);
					return false;
				}

				writes.Add((blockPos, customIndex));
			}
			else
			{
				writes.Add((blockPos, (ushort)cell.VanillaBlockIndex));
			}
		}

		foreach (var write in writes)
		{
			world.SetBlock(write.Pos, write.Block);
		}

		_domains.Log.LogInformation(
			"[StructurePlacement] mod-requested structure {Id} placed at block ({X},{Y}) ({CellCount} cells); the SetBlock relay will replicate each write.",
			structureId, originX, originY, writes.Count);
		return true;
	}

	bool IModLiquidPlacer.TryPlaceLiquid(string liquidTileId, int x, int y) =>
		_domains.LiquidTilePlacement.TryPlaceLiquid(liquidTileId, x, y);

	bool IModLiquidPlacer.TryFloodFill(string liquidTileId, int startX, int startY, int maxFill) =>
		_domains.LiquidTilePlacement.TryFloodFill(liquidTileId, startX, startY, maxFill);

	bool IModNativeApiProvider.IsRegistered(string operation) =>
		operation == ModNativeApiOperations.LocalPlayerState;

	bool IModNativeApiProvider.TryGetLocalPlayerState(out IModNativeLocalPlayerState state)
	{
		state = null!;

		var body = _domains.Run.LocalBody;
		if (body == null) // Unity object — == (scene-reload check)
		{
			return false;
		}

		var position = body.transform.position;
		state = new NativeLocalPlayerState(
			position.x,
			position.y,
			body.brainHealth,
			body.hunger,
			body.thirst,
			body.stamina,
			body.energy,
			body.temperature,
			body.consciousness,
			body.alive,
			body.conscious);
		return true;
	}

	private sealed class NativeLocalPlayerState(
		float x,
		float y,
		float brainHealth,
		float hunger,
		float thirst,
		float stamina,
		float energy,
		float temperature,
		float consciousness,
		bool alive,
		bool conscious) : IModNativeLocalPlayerState
	{
		public float X { get; } = x;

		public float Y { get; } = y;

		public float BrainHealth { get; } = brainHealth;

		public float Hunger { get; } = hunger;

		public float Thirst { get; } = thirst;

		public float Stamina { get; } = stamina;

		public float Energy { get; } = energy;

		public float Temperature { get; } = temperature;

		public float Consciousness { get; } = consciousness;

		public bool Alive { get; } = alive;

		public bool Conscious { get; } = conscious;
	}
}
