using System;
using System.Diagnostics;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Content;
using CasualtiesUnknownOnline.GameAdapter.Patches;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Diagnostics;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.Tutorial;
using CasualtiesUnknownOnline.Runtime.Session.World;
using HarmonyLib;
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
public sealed class GameAdapter : IGameAdapter, ICuoService, IModEntitySpawner, IModItemSpawner, IModTilePlacer, IModStructurePlacer, IModLiquidPlacer, IModNativeApiProvider, IPlayerInteractionVisibility
{
	/// <summary>
	/// Set when the game was launched via a Steam friends "Join Game"
	/// (+connect_lobby): the content-warning/intro screen is skipped so the
	/// menu is usable immediately — the follow-host pump needs PreRunScript.
	/// </summary>
	public static bool SkipIntro { get; set; }

	private readonly GameAdapterDomains _domains;
	private readonly GameAdapterBridge _bridge;
	private readonly PlayerInteractionApply _playerInteraction;
	private readonly RemoteInventoryOperationApply _remoteInventoryApply;
	private readonly GameAdapterSessionBinding _sessionBinding;
	private readonly LatencyInstrumentation _latency;
	private Harmony? _harmony;
	private Body? _lastLocalBody; // Unity object — == (the world-entry edge for the destroy-suppression reset)

	public GameAdapter(
		ISessionControl session,
		IEntitySyncControl entities,
		ICharacterDataControl characterData,
		IWorldControl world,
		IItemControl items,
		ICraftControl craft,
		ItemArbitration arbitration,
		EnemySyncService enemies,
		IWorldTimeControl worldTime,
		IPlayerInteractionControl playerInteraction,
		ITutorialClawControl tutorialClaw,
		IOptionsMonitor<RespawnOptions> respawnOptions,
		IHostRules hostRules,
		WorldEntityKernelProjection worldEntityKernel,
		IKernelProtocolControl kernelProtocol,
		ILogger<GameAdapter> log,
		LatencyInstrumentation latency,
		IMapper mapper,
		ILoggerFactory loggerFactory,
		GameAdapterItemContentProvider itemContent,
		GameAdapterBuildingContentProvider buildingContent,
		GameAdapterTileContentProvider tileContent,
		GameAdapterLiquidTileContentProvider liquidTileContent,
		GameAdapterStructureContentProvider structureContent,
		GameAdapterStatusContentProvider statusContent,
		GameAdapterMoodleContentProvider moodleContent,
		ModStatusStore modStatusStore)
	{
		_latency = latency;
		_domains = new GameAdapterDomains(session, entities, characterData, world, items, craft, arbitration,
			enemies, worldTime, playerInteraction, tutorialClaw, respawnOptions, hostRules, worldEntityKernel, kernelProtocol, log, mapper, loggerFactory, itemContent, buildingContent, tileContent, liquidTileContent, structureContent, statusContent, moodleContent, modStatusStore);
		_bridge = new GameAdapterBridge(_domains);
		_playerInteraction = new PlayerInteractionApply(_domains);
		_remoteInventoryApply = new RemoteInventoryOperationApply(_domains);
		var pushApply = new PlayerPushApply(_domains);
		_sessionBinding = new GameAdapterSessionBinding(_domains, _playerInteraction, _remoteInventoryApply, pushApply);
		PatchBridge.Bind(_bridge); // the only static seam — Harmony patches read the narrow surface, never this instance
	}

	public string CapabilityReport { get; private set; } = "Not probed";

	public bool ProbeGame()
	{
		var playerCamera = typeof(PlayerCamera);
		var body = typeof(Body);
		var preRun = typeof(PreRunScript);
		var worldGen = typeof(WorldGeneration);
		var ok = playerCamera is not null && body is not null && preRun is not null && worldGen is not null;
		CapabilityReport = ok
			? "PlayerCamera/Body/PreRunScript/WorldGeneration: OK"
			: "PlayerCamera/Body/PreRunScript/WorldGeneration: MISSING";
		return ok;
	}

	public bool Install()
	{
		try
		{
			_harmony = new Harmony("CasualtiesUnknownOnline.GameAdapter");
			_harmony.PatchAll(typeof(GameAdapter).Assembly);
			DynamicPatchInstaller.Install(_harmony, _domains.Log);

			// Never let a failed patch silently run: verify every patch class
			// actually landed on its target (a game update that breaks a target
			// must fail loud — a silently missing hook is how sync bugs hide).
			var missing = PatchInventory.VerifyMissing(_harmony);
			if (missing.Count > 0)
			{
				_domains.Log.LogError("Game Adapter patch verification FAILED — {Count} targets not applied: {Missing}",
					missing.Count, string.Join(", ", missing));
				_harmony.UnpatchSelf();
				_harmony = null;
				return false;
			}

			_domains.Log.LogInformation("Game Adapter patches installed and verified ({Count} targets).", PatchInventory.CountTargets());
			return true;
		}
		catch (Exception ex)
		{
			_domains.Log.LogError(ex, "Game Adapter patch install failed.");
			_harmony?.UnpatchSelf();
			_harmony = null;
			return false;
		}
	}

	public void Uninstall()
	{
		_harmony?.UnpatchSelf();
		_harmony = null;
	}

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

		using (_latency.Measure("WorldTime"))
		{
			_domains.WorldTimeSync.Update(); // host policy + direct-write adoption + resend; guest enforcement of the host speed
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

		_lastLocalBody = localBody;
		using (_latency.Measure("StartGate"))
		{
			_domains.Gate.Update(_domains.Run.LocalBody);
		}
		_domains.GenItemAuthority.Update(); // host/solo: publish the generation-time items when the generation finished
		_domains.GenItemApplication.Update(); // guest: apply the host's generation snapshot once the local generation finished
		using (_latency.Measure("Respawn"))
		{
			_domains.Respawn.Update(); // host: next-level respawn once a world generation finishes
		}
		_domains.TrapLayoutScanner.Update(); // host: report the generated trap layout on the same falling edge
		_domains.TrapLayoutApplication.Update(); // guest: apply a deferred layout snapshot once the local generation finished
		_domains.LayerModifierSync.Update(); // guest: apply the host's layer modifier once the local generation finished
		_domains.CarriedInventoryReporter.Update(); // guest: report the carried inventory with self-assigned ids once the local generation finished
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
		if (frameStopwatch != null)
		{
			_latency.RecordFrame(frameStopwatch.Elapsed.TotalMilliseconds);
		}

		_latency.Flush();
	}

	/// <summary>
	/// Late-frame carry presentation pass. Called from the plugin's
	/// <c>LateUpdate</c> after every game/cuo Update has run. This is the final
	/// carrier-side pin: every remote rider clone must sit on the local
	/// carrier's final body transform for the frame that is about to render,
	/// even if a game script (or the native body simulation) moved the carrier
	/// after <see cref="RemotePlayerRenderer.Update"/> or after
	/// <see cref="BodyUpdatePatch"/> re-pinned it.
	/// </summary>
	public void LateUpdateCarryPresentation()
	{
		var localBody = _domains.Run.LocalBody;
		if (localBody == null) // Unity object — ==
		{
			return;
		}

		_domains.Renderer.RefreshLocalCarrierAttach(localBody);
	}

	void ICuoService.Stop() => Uninstall();

	void IDisposable.Dispose()
	{
		_domains.WorldTimeSync.Unbind();
		_sessionBinding.Unbind();
		_domains.Renderer.DestroyAllClones();
		PatchBridge.Unbind(_bridge);
	}

	// ---- IGameAdapter ----

	bool IGameAdapter.IsWaitingForReady => _domains.Gate.WaitingForReady;

	bool IGameAdapter.IsInWorldOrGenerating => _domains.Run.IsInWorldOrGenerating;

	string IGameAdapter.WaitingText => _domains.Gate.WaitingText;

	void IGameAdapter.CaptureWorldParams() => _domains.WorldParams.CaptureAtBoundary();

	void IGameAdapter.ApplyWorldParams(WorldStartParams parameters) => _domains.WorldParams.Apply(parameters);

	void IGameAdapter.OnApplicationQuit() => _domains.ItemWorldSync.SuppressDestroys();

	bool IPlayerInteractionVisibility.HasLineOfSight(ulong observerSteamId, ulong targetSteamId) =>
		_domains.InteractionVisibility.HasLineOfSight(observerSteamId, targetSteamId);

	bool IGameAdapter.HasLocalHealItem() => _playerInteraction.HasLocalHealItem();

	IReadOnlyList<LocalHealItem> IGameAdapter.GetLocalHealItems() => _playerInteraction.GetLocalHealItems();

	bool IGameAdapter.TryRequestTraderRecruit(ulong targetSteamId) => _domains.TraderRecruit.TryRequest(targetSteamId);

	void IGameAdapter.SetOnlineUiModal(bool visible) => _domains.MenuInput.SetModal(visible);

	void IGameAdapter.SetOnlineUiEscapeSurfaceVisible(bool visible) =>
		_domains.MenuInput.SetNonModalEscapeSurfaceVisible(visible);

	void IGameAdapter.SetOnlineUiScopedBlocks(IReadOnlyList<OnlineUiBlockRect> blocks) =>
		_domains.MenuInput.SetScopedBlocks(blocks);

	bool IGameAdapter.OpenRemoteBackpack(ulong targetSteamId, string displayName) =>
		_domains.RemoteBackpack.Open(targetSteamId, displayName);

	void IGameAdapter.CloseRemoteBackpack() => _domains.RemoteBackpack.Close();

	bool IGameAdapter.OpenRemoteMedical(ulong targetSteamId, string displayName) =>
		_domains.RemoteMedical.Open(targetSteamId, displayName);

	void IGameAdapter.CloseRemoteMedical() => _domains.RemoteMedical.Close();

	bool IGameAdapter.TryGetRemoteHeadPosition(ulong steamId, out float x, out float y)
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

	bool IModNativeApiProvider.TryInvoke(string operation, object?[] arguments, out object? result)
	{
		result = null;

		if (operation != ModNativeApiOperations.LocalPlayerState || arguments.Length != 0)
		{
			return false;
		}

		var body = _domains.Run.LocalBody;
		if (body == null) // Unity object — == (scene-reload check)
		{
			return false;
		}

		var position = body.transform.position;
		result = new NativeLocalPlayerState(
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
