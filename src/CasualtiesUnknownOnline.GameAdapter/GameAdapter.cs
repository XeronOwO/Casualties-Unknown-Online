using System;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Patches;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.World;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using UnityEngine;
using IGameAdapter = CasualtiesUnknownOnline.Runtime.GameAdapter.IGameAdapter;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Game Adapter for the current Casualties Unknown (Demo) build (architecture.md
/// §4). The only layer that knows game types. Thin coordinator: owns the
/// lifecycle (probe/install/uninstall), the Update pump (orchestrating the
/// domain pumps) and the two boundary interfaces — IPatchBridge (the narrow
/// surface the Harmony patches reach; one-line forwards to the domains) and
/// IGameAdapter (the Runtime's contract). The domain logic lives in
/// ItemWorldSync / CharacterDataSync / WorldEventSync / RunCoordinator /
/// RemotePlayerRenderer (each one responsibility, state owned internally).
/// </summary>
public sealed partial class GameAdapter : IGameAdapter, ICuoService, IPatchBridge
{
	/// <summary>
	/// Set when the game was launched via a Steam friends "Join Game"
	/// (+connect_lobby): the content-warning/intro screen is skipped so the
	/// menu is usable immediately — the follow-host pump needs PreRunScript.
	/// </summary>
	public static bool SkipIntro { get; set; }

	public string CapabilityReport { get; private set; } = "Not probed";

	/// <summary>Host in a live session: authoritative world mutations (damage table capture).</summary>
	internal bool IsHostMode => _session.Role == SessionRole.Host && _session.SessionActive;

	bool IPatchBridge.IsSessionActive => _session.SessionActive;

	bool IPatchBridge.IsHostMode => IsHostMode;

	void IPatchBridge.OnTrapTriggered(EntityEventKind kind, Vector2 position, byte extra) =>
		_entityEventSync.OnTrapTriggered(kind, position, extra);

	void IPatchBridge.OnEntityInstantiated(BuildingEntity entity)
	{
		// Enemy copies freeze at their spawn position before any AI/physics
		// moves them; the generic spawn channel then reports the creation.
		if (entity.animal)
		{
			_enemySync.OnAnimalInstantiated(entity);
		}

		_entitySpawnSync.OnEntityInstantiated(entity); // the spawn-channel report (runtime creations; creation-time data rides the same message, #128)
	}

	/// <summary>Patches whose target types are INTERNAL to the game assembly (no
	/// compile-time reference possible) — the installer reflects the type and
	/// patches the method directly (DynamicPatchInstaller, split out at the
	/// 600-line gate); the adapter owns the Harmony instance so it installs
	/// them beside PatchAll.</summary>
	private void InstallDynamicPatches() => DynamicPatchInstaller.Install(_harmony!, _log);

	bool IPatchBridge.IsReplayingLifePodSound => _lifePod.IsReplayingSound;

	/// <summary>Generation is isolated unconditionally (solo too) — that is what
	/// makes the captured Random.state reproducible, and therefore what makes
	/// mid-session joining work.</summary>
	bool IPatchBridge.IsWorldGenIsolated => true;

	bool IPatchBridge.IsInGateWindow => _run.IsInGateWindow;

	bool IPatchBridge.IsWaitingForReady => _gate.WaitingForReady;

	bool IPatchBridge.TryDeferStartGateAlert(string text, bool important) => _gate.DeferAlert(text, important);

	bool IGameAdapter.IsWaitingForReady => _gate.WaitingForReady;

	bool IGameAdapter.IsInWorldOrGenerating => _run.IsInWorldOrGenerating;

	string IGameAdapter.WaitingText => _gate.WaitingText;

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
			InstallDynamicPatches();

			// Never let a failed patch silently run: verify every patch class
			// actually landed on its target (a game update that breaks a target
			// must fail loud — a silently missing hook is how sync bugs hide).
			var missing = PatchInventory.VerifyMissing(_harmony);
			if (missing.Count > 0)
			{
				_log.LogError("Game Adapter patch verification FAILED — {Count} targets not applied: {Missing}",
					missing.Count, string.Join(", ", missing));
				_harmony.UnpatchSelf();
				_harmony = null;
				return false;
			}

			_log.LogInformation("Game Adapter patches installed and verified ({Count} targets).", PatchInventory.CountTargets());
			return true;
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "Game Adapter patch install failed.");
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
		BindToSession();
		if (ProbeGame())
		{
			Install();
		}
		else
		{
			_log.LogError("Game Adapter probe failed — CUO multiplayer unavailable.");
		}
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update()
	{
		_guestMenu.Update();
		_run.Update();
		_worldTimeSync.Update(); // host policy + direct-write adoption + resend; guest enforcement of the host speed

		// World-entry edge: the teardown of the PREVIOUS scene finished (its
		// destroys were suppressed, #191) — the new world's real destroys report
		// again. The edge rides the local body (null in any menu scene; Unity
		// object — ==).
		var localBody = _run.LocalBody;
		if (localBody != null && _lastLocalBody == null) // Unity objects — ==
		{
			_itemWorldSync.ResetDestroySuppression();
		}

		_lastLocalBody = localBody;
		UpdateCarriedBody(localBody); // a carried local body follows its carrier's entity state
		_gate.Update(_run.LocalBody);
		_genItemAuthority.Update(); // host/solo: publish the generation-time items when the generation finished
		_genItemApplication.Update(); // guest: apply the host's generation snapshot once the local generation finished
		_trapLayoutScanner.Update(); // host: report the generated trap layout on the same falling edge
		_trapLayoutApplication.Update(); // guest: apply a deferred layout snapshot once the local generation finished
		_layerModifierSync.Update(); // guest: apply the host's layer modifier once the local generation finished
		_carriedInventoryReporter.Update(); // guest: report the carried inventory with self-assigned ids once the local generation finished
		_itemWorldSync.FlushPendingDrop(); // a drop that was not thrown reports at end of frame (one drop = one report)
		_blockBreakSync.FlushPendingBlockBreak(); // a break's drops fold in one frame after the break — the break + drops go out as ONE message
		if (IsHostMode)
		{
			_itemPositionAuthority.Update(); // the host's physics is the single position authority
		}
		else
		{
			_itemPositionFollow.Update(); // the guest copies simulate locally (ground-layer isolation), soft-corrected by the host's stream
		}

		_worldEventSync.Update();
		_geyserStateSync.Update(); // host/solo: capture + broadcast the geysers' liquid types once the generation finished
		_entitySpawnSync.Update(); // the creation channel's deferred reports (a geyser's type, after its child Start) and carried-data applications
		_fluidSync.Update(); // host: stream the members' fluid viewports (10 Hz diff + 1 Hz full)
		_tradeSync.Update(); // host: the 5 s trader-state fallback broadcast
		_blockBreakSync.Update(); // expire break records without a consuming drops report
		_renderer.Update();
		_enemySync.Update(); // host: capture + publish the simulated enemies; guest: (event-driven bind/apply)
		_enemyCombat.Update(); // host: enemy combat decisions (target guidance rides the patch callbacks; bite arbitration here)
		_tutorialClawSync.Update(); // host: publish the tutorial-claw presentation state (Runtime throttles the 20 Hz fan-out)
	}

	void ICuoService.Stop() => Uninstall();

	void IDisposable.Dispose()
	{
		_worldTimeSync.Unbind();
		UnbindFromSession();
		_renderer.DestroyAllClones();
		PatchBridge.Unbind(this);
	}

	// ---- Session wiring ----

	internal void BindToSession()
	{
		_characterDataSync.BindToSession();
		_renderer.BindToSession();
		_itemApplication.BindToSession();
		_itemReconcile.BindToSession();
		_itemWorldSync.BindToSession();
		_itemPositionFollow.BindToSession();
		_worldEventSync.BindToSession();
		_entityEventSync.BindToSession();
		_entitySpawnSync.BindToSession();
		_geyserStateSync.BindToSession();
		_fluidSync.BindToSession();
		_tradeSync.BindToSession();
		_speechSync.BindToSession();
		_recipeUnlockApply.BindToSession();
		_enemySync.BindToSession();
		_enemyProximity.BindToSession();
		_characterSoundSync.BindToSession();
		_tutorialClawSync.BindToSession();
		_worldTimeSync.BindToSession();
		_run.BindToSession();
		_session.SessionEnded += OnSessionEnded;
		_genItemApplication.BindToSession();
		_trapLayoutApplication.BindToSession();
		_layerModifierSync.BindToSession();
		_items.ItemCarriedSyncReceived += OnItemCarriedSync; // the owner's clone re-renders the moment a carried fact changes
		_items.ItemDropped += OnCarriedItemDropped; // a carried item leaving into the world leaves the fact table (recursive)
		_items.ItemIdWatermarkReceived += OnItemIdWatermark; // the host granted the id counter — resume from watermark + 1
		_items.CarriedInventoryReceived += OnCarriedInventory; // a guest's starting supplies with self-assigned ids — seed the fact table (clone render + divergence baseline)
		_playerInteraction.TransferReceived += OnPlayerInventoryTransfer; // cross-player take: apply the local body mutation and re-report
		_playerInteraction.CarryStateChanged += OnCarryStateChanged; // cross-player carry: set/clear the local carried-body driver
		_playerInteraction.HealReceived += OnPlayerHealReceived; // cross-player heal: consume the local item and/or apply the target's post-heal state
	}

	private void UnbindFromSession()
	{
		_characterDataSync.Unbind();
		_renderer.Unbind();
		_itemApplication.Unbind();
		_itemReconcile.Unbind();
		_itemWorldSync.Unbind();
		_itemWorldSync.ResetPending(); // session ended — a pending drop cannot resolve anymore
		_blockBreakSync.ResetPending(); // a pending break's drops are gone with the world
		_itemPositionFollow.Unbind();
		_worldEventSync.Unbind();
		_entityEventSync.Unbind();
		_entitySpawnSync.Unbind();
		_geyserStateSync.Unbind();
		_fluidSync.Unbind();
		_tradeSync.Unbind();
		_speechSync.Unbind();
		_recipeUnlockApply.Unbind();
		_enemySync.Unbind();
		_enemyProximity.Unbind();
		_characterSoundSync.Unbind();
		_tutorialClawSync.Unbind();
		_craftingSync.ResetPending(); // the destroy claims die with the scene
		_run.Unbind();
		_session.SessionEnded -= OnSessionEnded;
		_genItemApplication.Unbind();
		_trapLayoutApplication.Unbind();
		_layerModifierSync.Unbind();
		_items.ItemCarriedSyncReceived -= OnItemCarriedSync;
		_items.ItemDropped -= OnCarriedItemDropped;
		_items.ItemIdWatermarkReceived -= OnItemIdWatermark;
		_items.CarriedInventoryReceived -= OnCarriedInventory;
		_playerInteraction.TransferReceived -= OnPlayerInventoryTransfer;
		_playerInteraction.CarryStateChanged -= OnCarryStateChanged;
		_playerInteraction.HealReceived -= OnPlayerHealReceived;
	}

	// ---- IGameAdapter ----

	void IGameAdapter.CaptureWorldParams() => _worldParams.CaptureAtBoundary();

	void IGameAdapter.ApplyWorldParams(WorldStartParams parameters) => _worldParams.Apply(parameters);


	// ---- IPatchBridge: one-line forwards to the owning domain ----

	void IPatchBridge.OnWorldGenerate()
	{
		_run.OnWorldGenerate();
		_gate.AttachKeepLoading(); // both roles: while the gate waits for the others, the loading animation keeps playing
								   // The tutorial/debug layers skip the game's ResetLayerModifiers
								   // (WorldGeneration.cs:3280-3283 — it only runs for biomeOverride == None),
								   // so the previous layer's modifier keeps its static active state: a run
								   // with a modifier followed by the tutorial showed no banner (the tutorial
								   // never rolls one, WorldGeneration.cs:3626-3628) but the modifier's
								   // effects still ran. Clear it on EVERY side — the modifier instances are
								   // process-static (LayerModifier.availableModifiers), one set per game;
								   // the guest's active was set by the snapshot path.
		if (WorldGeneration.world != null && HarmonyTraverse.ReadBiomeOverride() != 0) // Unity object — ==; 0 = None
		{
			WorldGeneration.world.ResetLayerModifiers();
			_log.LogInformation("[LayerMod] non-none layer — cleared the previous layer's residual modifiers.");
		}

		if (_session.Role != SessionRole.Guest)
		{
			// A new world/layer is generating — the old layer's world items are
			// gone with the scene; the authoritative table starts empty again
			// (regression guard: this call lived inside the old GameAdapter's
			// OnWorldGenerate and was lost in the domain split).
			_items.ResetItems();
			_itemWorldSync.ResetPending(); // a pending drop's item is gone with the old layer
			_blockBreakSync.ResetPending(); // a pending break's drops are gone with the old layer
		}
	}

	void IPatchBridge.OnBlockSet(Vector2Int pos, ushort block) => _worldEventSync.OnBlockSet(pos, block);

	void IPatchBridge.OnBlockDamaged(Vector2 pos, float dmg, bool bonusMetal) => _blockBreakSync.OnBlockDamaged(pos, dmg, bonusMetal);

	void IPatchBridge.OnBuildingEntityDamaged(BuildingEntity entity, float damage, bool playHitSound)
	{
		_worldEventSync.OnBuildingEntityDamaged(entity, damage, playHitSound);
		_enemySync.RecordLocalAttack(entity, damage); // a guest's local drop on a frozen enemy must not flash-revert on the next batch
	}

	void IPatchBridge.OnBuildingEntityOpened(BuildingEntity entity) =>
		_worldEventSync.OnBuildingEntityOpened(entity);

	void IPatchBridge.DeferLifePodSound() => _lifePod.DeferSound();

	void IPatchBridge.DeferLifePodShake() => _lifePod.DeferShake();

	bool IPatchBridge.OnGuestStartAttempt() => _guestMenu.OnGuestStartAttempt();

	void IPatchBridge.OnWorldJoinRequested(bool isTutorial) => _run.OnWorldJoinRequested(isTutorial);

	void IPatchBridge.OnSceneLoadBegin() => _itemWorldSync.SuppressDestroys();

	void IGameAdapter.OnApplicationQuit() => _itemWorldSync.SuppressDestroys();

	void IPatchBridge.OnInventoryChanged() => _characterDataSync.ReportInventoryChanged(_run.LocalBody);

	bool IPatchBridge.EnsureGuestWorldParams() => _worldParams.EnsureGuestApplied();

	void IPatchBridge.ResetGenStreamToBaseline() => _worldParams.ResetGenStreamToBaseline();

	void IPatchBridge.OnEarthquakeStarted(float duration, float nextDelay) =>
		_worldEventSync.OnEarthquakeStarted(duration, nextDelay);

	void IPatchBridge.OnDarkenSkipped() =>
		_log.LogInformation("[Gate] fade skipped while the gate window holds.");

	void IPatchBridge.OnPickupCheckFailed(string itemId, float distance, bool blocked) =>
		_log.LogWarning("[PickupCheck] {Item} refused — distance {Distance:F1}, line-of-sight blocked {Blocked}.", itemId, distance, blocked);

	void IPatchBridge.OnDragReleasedToWorld() =>
		_log.LogWarning("[DragFlow] release fell through to the WORLD path (no UI target hit).");

	void IPatchBridge.OnPickUpResult(string itemId, int slot, string home, Vector2 position) =>
		_log.LogInformation("[PickUpResult] {Item} → {Home} (slot {Slot}) at ({X:F1},{Y:F1}).", itemId, home, slot, position.x, position.y);

	bool IPatchBridge.ShouldApplyQuakeBreak(Vector2Int blockPos)
	{
		var world = WorldGeneration.world;
		if (world == null || world.earthquakeTime <= 0f) // Unity object — ==; only quake breaks are gated (environment breaks pass)
		{
			return true;
		}

		// Numbering = SteamId order (globally consistent on every side). A
		// break applies only when it is far (> 60 blocks) from every EARLIER
		// numbered player — their region already covers it (their own breaks
		// run, and the last-numbered player's region has no overlap left).
		// Overlapping players therefore keep the total break rate at solo
		// level ("two players standing together break faster" is fixed);
		// separated players each keep the full solo rate.
		var earlier = _session.Members
			.Where(m => m.SteamId < _session.LocalSteamId)
			.ToList();
		if (earlier.Count == 0)
		{
			return true; // no earlier player — this side owns its region
		}

		foreach (var member in earlier)
		{
			var remote = _entities.GetRemotePlayer(member.SteamId);
			if (remote is null)
			{
				continue;
			}

			var playerBlock = world.WorldToBlockPos(new Vector2(remote.Position.X, remote.Position.Y));
			if (Vector2Int.Distance(blockPos, playerBlock) < 60)
			{
				return false; // covered by an earlier player — skip this break
			}
		}

		return true;
	}

	void IPatchBridge.OnItemInstantiated(Item item) => _itemWorldSync.OnItemInstantiated(item);

	void IPatchBridge.OnBrokenItemUpdate(Item item, string reason) => _itemWorldSync.OnBrokenItemUpdate(item, reason);

	void IPatchBridge.OnItemDestroyed(Item item) => _itemWorldSync.OnItemDestroyed(item);

	void IPatchBridge.OnItemPickupStart(Item item) => _pickupSync.OnPickupStart(item);

	void IPatchBridge.OnItemPickedUp(Item item) => _pickupSync.OnPickedUp(item);

	void IPatchBridge.OnItemDropped(Item item) => _itemWorldSync.OnItemDropped(item);

	void IPatchBridge.OnItemThrown(Item item) => _itemWorldSync.OnItemThrown(item);

	void IPatchBridge.OnItemLoadedIntoContainer(Item item, bool wasWorldItem) =>
		_containerSync.OnLoadedIntoContainer(item, wasWorldItem);

	void IPatchBridge.OnItemUnloadedFromContainer(Item item) => _containerSync.OnUnloadedFromContainer(item);

	void IPatchBridge.OnContainerUnloadedAll(Container container) => _containerSync.OnUnloadedAll(container);

	void IPatchBridge.OnItemUsed(Item item)
	{
		_itemUseSync.OnItemUsed(item);
		_craftingSync.OnItemUsed(item); // a blueprint use unlocks its recipe (the unlock fact — the destruction rides the use digest)
	}

	object? IPatchBridge.OnCraftBegin(Recipe recipe) => _craftingSync.OnCraftBegin(recipe);

	void IPatchBridge.OnCraftEnd(object? state) => _craftingSync.OnCraftEnd(state);

	object? IPatchBridge.OnCombineBegin(Body body, Item it1, Item it2) => _craftingSync.OnCombineBegin(body, it1, it2);

	void IPatchBridge.OnCombineEnd(object? state) => _craftingSync.OnCombineEnd(state);

	void IPatchBridge.OnLiquidTransferFinished(WaterContainerItem transferTo, WaterContainerItem transferFrom) =>
		_craftingSync.OnLiquidTransferFinished(transferTo, transferFrom);

	bool IPatchBridge.ShouldSuppressDestroy(Item item) =>
		_craftingSync.ShouldSuppressDestroy(item) || _heaterCookSync.ShouldSuppressDestroy(item);

	void IPatchBridge.OnSlotMoved(Body body, int slot, string origin) => _itemSlotSync.OnSlotMoved(body, slot, origin);

	void IPatchBridge.OnItemWorn(Item item) => _itemSlotSync.OnItemWorn(item);

	void IPatchBridge.OnFluidFixedUpdate() => _fluidSync.OnFluidFixedUpdate();

	void IPatchBridge.OnFluidDrinkReported(Vector2Int pos) => _fluidSync.OnDrinkReported(pos);

	void IPatchBridge.OnTraderActionReported(TraderScript trader, TraderActionKind action, string itemId, int itemValue, Item? purchaseItem) =>
		_tradeSync.OnTraderActionReported(trader, action, itemId, itemValue, purchaseItem);

	void IPatchBridge.OnSpeechReported(Talker talker, string text) => _speechSync.OnSpeechReported(talker, text);
}
