using System;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.GameAdapter.Patches;
using CasualtiesUnknownOnline.GameAdapter.WorldGen;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using HarmonyLib;
using MapsterMapper;
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
public sealed class GameAdapter : IGameAdapter, ICuoService, IPatchBridge
{
	/// <summary>
	/// Set when the game was launched via a Steam friends "Join Game"
	/// (+connect_lobby): the content-warning/intro screen is skipped so the
	/// menu is usable immediately — the follow-host pump needs PreRunScript.
	/// </summary>
	public static bool SkipIntro { get; set; }

	private readonly SessionService _session;
	private readonly ItemService _items;
	private readonly EntitySyncService _entities;
	private readonly ILogger<GameAdapter> _log;
	private Harmony? _harmony;

	private readonly CharacterDataSync _characterDataSync;
	private readonly RemotePlayerRenderer _renderer;
	private readonly DropProtectionGuard _dropGuard;
	private readonly ItemApplication _itemApplication;
	private readonly ItemWorldSync _itemWorldSync;
	private readonly PickupSync _pickupSync;
	private readonly ContainerItemSync _containerSync;
	private readonly OperationTrace _operationTrace;
	private readonly ItemPositionAuthority _itemPositionAuthority;
	private readonly ItemPositionFollow _itemPositionFollow;
	private readonly WorldEventSync _worldEventSync;
	private readonly LifePodPresentation _lifePod;
	private readonly RunCoordinator _run;
	private readonly WorldParamsService _worldParams;
	private readonly StartGateCoordinator _gate;
	private readonly GuestMenuGuard _guestMenu;

	public GameAdapter(SessionService session, EntitySyncService entities, CharacterDataStore characterData,
		WorldService world, ItemService items, ILogger<GameAdapter> log, IMapper mapper, ILoggerFactory loggerFactory)
	{
		_session = session;
		_items = items;
		_entities = entities;
		_log = log;
		// Domains (state belongs to its owner; the coordinator forwards, never holds).
		// Construction order follows the dependencies: item domains (guard → world →
		// application → authority/follow, one-way), run → gate (the gate reads the
		// run's phase machine).
		ItemStateCodec.BindLog(log);
		WorldGenRandomIsolation.Log = msg => _log.LogInformation(msg); // generation-stream segment fingerprints (peer log comparison)
		_characterDataSync = new CharacterDataSync(session, characterData, mapper, loggerFactory.CreateLogger<CharacterDataSync>());
		_renderer = new RemotePlayerRenderer(session, entities, _characterDataSync, loggerFactory.CreateLogger<RemotePlayerRenderer>());
		_dropGuard = new DropProtectionGuard();
		_itemApplication = new ItemApplication(items, _dropGuard, loggerFactory.CreateLogger<ItemApplication>());
		_operationTrace = new OperationTrace(loggerFactory.CreateLogger<OperationTrace>());
		var itemReports = new ItemReportCommitter(items, _operationTrace, loggerFactory.CreateLogger<ItemReportCommitter>());
		var itemIds = new ItemIdAllocator(session);
		var itemDropState = new ItemDropState();
		_itemWorldSync = new ItemWorldSync(session, items, _dropGuard, itemDropState, _operationTrace, itemReports, itemIds, loggerFactory.CreateLogger<ItemWorldSync>());
		_pickupSync = new PickupSync(items, _itemApplication, itemDropState, itemIds, _operationTrace, itemReports);
		_containerSync = new ContainerItemSync(items, itemDropState, itemIds, _operationTrace, itemReports, loggerFactory.CreateLogger<ContainerItemSync>());
		_itemPositionAuthority = new ItemPositionAuthority(items);
		_itemPositionFollow = new ItemPositionFollow(items, _dropGuard);
		_worldEventSync = new WorldEventSync(session, world, _operationTrace, loggerFactory.CreateLogger<WorldEventSync>());
		_lifePod = new LifePodPresentation(loggerFactory.CreateLogger<LifePodPresentation>());
		_guestMenu = new GuestMenuGuard(session, loggerFactory.CreateLogger<GuestMenuGuard>());
		_worldParams = new WorldParamsService(world, loggerFactory.CreateLogger<WorldParamsService>());
		_run = new RunCoordinator(session, world, entities, _characterDataSync, _guestMenu, _worldParams, loggerFactory.CreateLogger<RunCoordinator>());
		_gate = new StartGateCoordinator(session, world, _lifePod, _run, loggerFactory.CreateLogger<StartGateCoordinator>());
		PatchBridge.Bind(this); // the only static seam — Harmony patches read the narrow surface, never this instance
	}

	public string CapabilityReport { get; private set; } = "Not probed";

	/// <summary>Host in a live session: authoritative world mutations (damage table capture).</summary>
	internal bool IsHostMode => _session.Role == SessionRole.Host && _session.SessionActive;

	bool IPatchBridge.IsSessionActive => _session.SessionActive;

	bool IPatchBridge.IsHostMode => IsHostMode;

	bool IPatchBridge.IsReplayingLifePodSound => _lifePod.IsReplayingSound;

	/// <summary>Generation is isolated unconditionally (solo too) — that is what
	/// makes the captured Random.state reproducible, and therefore what makes
	/// mid-session joining work.</summary>
	bool IPatchBridge.IsWorldGenIsolated => true;

	bool IPatchBridge.IsInGateWindow => _run.IsInGateWindow;

	bool IPatchBridge.IsWaitingForReady => _gate.WaitingForReady;

	bool IGameAdapter.IsWaitingForReady => _gate.WaitingForReady;

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
		_gate.Update(_run.LocalBody);
		_itemWorldSync.FlushPendingDrop(); // a drop that was not thrown reports at end of frame (one drop = one report)
		if (IsHostMode)
		{
			_itemPositionAuthority.Update(); // the host's physics is the single position authority
		}
		else
		{
			_itemPositionFollow.Update(); // the guest copies are kinematic renders of it
		}

		_worldEventSync.Update();
		_renderer.Update();
	}

	void ICuoService.Stop() => Uninstall();

	void IDisposable.Dispose()
	{
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
		_itemWorldSync.BindToSession();
		_itemPositionFollow.BindToSession();
		_worldEventSync.BindToSession();
		_run.BindToSession();
	}

	private void UnbindFromSession()
	{
		_characterDataSync.Unbind();
		_renderer.Unbind();
		_itemApplication.Unbind();
		_itemWorldSync.Unbind();
		_itemWorldSync.ResetPending(); // session ended — a pending drop cannot resolve anymore
		_itemPositionFollow.Unbind();
		_worldEventSync.Unbind();
		_run.Unbind();
	}

	// ---- IGameAdapter ----

	void IGameAdapter.CaptureWorldParams() => _worldParams.CaptureAtBoundary();

	void IGameAdapter.ApplyWorldParams(WorldStartParams parameters) => _worldParams.Apply(parameters);


	// ---- IPatchBridge: one-line forwards to the owning domain ----

	void IPatchBridge.OnWorldGenerate()
	{
		_run.OnWorldGenerate();
		_gate.AttachKeepLoading(); // both roles: while the gate waits for the others, the loading animation keeps playing
		if (_session.Role != SessionRole.Guest)
		{
			// A new world/layer is generating — the old layer's world items are
			// gone with the scene; the authoritative table starts empty again
			// (regression guard: this call lived inside the old GameAdapter's
			// OnWorldGenerate and was lost in the domain split).
			_items.ResetItems();
			_itemWorldSync.ResetPending(); // a pending drop's item is gone with the old layer
		}
	}

	void IPatchBridge.OnBlockSet(Vector2Int pos, ushort block) => _worldEventSync.OnBlockSet(pos, block);

	void IPatchBridge.OnBlockDamaged(Vector2 pos, float dmg) => _worldEventSync.OnBlockDamaged(pos, dmg);

	void IPatchBridge.OnBuildingEntityDamaged(BuildingEntity entity, float damage) =>
		_worldEventSync.OnBuildingEntityDamaged(entity, damage);

	void IPatchBridge.OnBuildingEntityOpened(BuildingEntity entity) =>
		_worldEventSync.OnBuildingEntityOpened(entity);

	void IPatchBridge.DeferLifePodSound() => _lifePod.DeferSound();

	void IPatchBridge.DeferLifePodShake() => _lifePod.DeferShake();

	bool IPatchBridge.OnGuestStartAttempt() => _guestMenu.OnGuestStartAttempt();

	void IPatchBridge.OnWorldJoinRequested(bool isTutorial) => _run.OnWorldJoinRequested(isTutorial);

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

	void IPatchBridge.OnItemDestroyed(Item item) => _itemWorldSync.OnItemDestroyed(item);

	void IPatchBridge.OnItemPickupStart(Item item) => _pickupSync.OnPickupStart(item);

	void IPatchBridge.OnItemPickedUp(Item item) => _pickupSync.OnPickedUp(item);

	void IPatchBridge.OnItemDropped(Item item) => _itemWorldSync.OnItemDropped(item);

	void IPatchBridge.OnItemThrown(Item item) => _itemWorldSync.OnItemThrown(item);

	void IPatchBridge.OnItemLoadedIntoContainer(Item item, bool wasWorldItem) =>
		_containerSync.OnLoadedIntoContainer(item, wasWorldItem);

	void IPatchBridge.OnItemUnloadedFromContainer(Item item) => _containerSync.OnUnloadedFromContainer(item);

	void IPatchBridge.OnContainerUnloadedAll(Container container) => _containerSync.OnUnloadedAll(container);
}
