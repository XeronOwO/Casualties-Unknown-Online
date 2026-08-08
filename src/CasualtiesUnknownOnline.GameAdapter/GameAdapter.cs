using System;
using CasualtiesUnknownOnline.Abstractions;
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
	private readonly ILogger<GameAdapter> _log;
	private Harmony? _harmony;

	private readonly CharacterDataSync _characterDataSync;
	private readonly RemotePlayerRenderer _renderer;
	private readonly ItemApplication _itemApplication;
	private readonly ItemWorldSync _itemWorldSync;
	private readonly ItemPositionSync _positionSync;
	private readonly WorldEventSync _worldEventSync;
	private readonly LifePodPresentation _lifePod;
	private readonly RunCoordinator _run;
	private readonly StartGateCoordinator _gate;
	private readonly GuestMenuGuard _guestMenu;

	public GameAdapter(SessionService session, EntitySyncService entities, CharacterDataStore characterData,
		WorldService world, ItemService items, ILogger<GameAdapter> log, IMapper mapper, ILoggerFactory loggerFactory)
	{
		_session = session;
		_log = log;
		// Domains (state belongs to its owner; the coordinator forwards, never holds).
		// Construction order follows the dependencies: itemWorld → positionSync,
		// run → gate (the gate reads the run's phase machine).
		ItemStateCodec.BindLog(log);
		_characterDataSync = new CharacterDataSync(session, characterData, mapper, loggerFactory.CreateLogger<CharacterDataSync>());
		_renderer = new RemotePlayerRenderer(session, entities, _characterDataSync, loggerFactory.CreateLogger<RemotePlayerRenderer>());
		_itemApplication = new ItemApplication(items, loggerFactory.CreateLogger<ItemApplication>());
		_itemWorldSync = new ItemWorldSync(session, items, _itemApplication, loggerFactory.CreateLogger<ItemWorldSync>());
		_positionSync = new ItemPositionSync(session, items, _itemApplication, loggerFactory.CreateLogger<ItemPositionSync>());
		_worldEventSync = new WorldEventSync(session, world, loggerFactory.CreateLogger<WorldEventSync>());
		_lifePod = new LifePodPresentation(loggerFactory.CreateLogger<LifePodPresentation>());
		_guestMenu = new GuestMenuGuard(session, loggerFactory.CreateLogger<GuestMenuGuard>());
		_run = new RunCoordinator(session, world, entities, _characterDataSync, _guestMenu, loggerFactory.CreateLogger<RunCoordinator>());
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
			_log.LogInformation("Game Adapter patches installed.");
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
		_positionSync.Update();
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
		_positionSync.BindToSession();
		_worldEventSync.BindToSession();
		_run.BindToSession();
	}

	private void UnbindFromSession()
	{
		_characterDataSync.Unbind();
		_renderer.Unbind();
		_itemApplication.Unbind();
		_positionSync.Unbind();
		_worldEventSync.Unbind();
		_run.Unbind();
	}

	// ---- IGameAdapter ----

	void IGameAdapter.CaptureWorldParams() => _run.CaptureWorldParams();

	void IGameAdapter.ApplyWorldParams(WorldStartParams parameters) => _run.ApplyWorldParams(parameters);

	// ---- IPatchBridge: one-line forwards to the owning domain ----

	void IPatchBridge.OnWorldGenerate() => _run.OnWorldGenerate();

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

	bool IPatchBridge.EnsureGuestWorldParams() => _run.EnsureGuestWorldParams();

	void IPatchBridge.ResetGenStreamToBaseline() => _run.ResetGenStreamToBaseline();

	void IPatchBridge.OnEarthquakeStarted(float duration, float nextDelay) =>
		_worldEventSync.OnEarthquakeStarted(duration, nextDelay);

	void IPatchBridge.OnItemInstantiated(Item item) => _itemWorldSync.OnItemInstantiated(item);

	void IPatchBridge.OnItemDestroyed(Item item) => _itemWorldSync.OnItemDestroyed(item);

	void IPatchBridge.OnItemPickupStart(Item item) => _itemWorldSync.OnItemPickupStart(item);

	void IPatchBridge.OnItemPickedUp(Item item) => _itemWorldSync.OnItemPickedUp(item);

	void IPatchBridge.OnItemDropped(Item item) => _itemWorldSync.OnItemDropped(item);

	void IPatchBridge.OnItemThrown(Item item) => _itemWorldSync.OnItemThrown(item);

	void IPatchBridge.OnItemLoadedIntoContainer(Item item, bool wasWorldItem) =>
		_itemWorldSync.OnItemLoadedIntoContainer(item, wasWorldItem);

	void IPatchBridge.OnItemUnloadedFromContainer(Item item) => _itemWorldSync.OnItemUnloadedFromContainer(item);

	void IPatchBridge.OnContainerUnloadedAll(Container container) => _itemWorldSync.OnContainerUnloadedAll(container);
}
