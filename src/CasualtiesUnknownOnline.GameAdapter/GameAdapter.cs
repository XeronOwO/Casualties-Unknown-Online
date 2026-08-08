using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.GameAdapter.Rendering;
using CasualtiesUnknownOnline.GameAdapter.WorldGen;
using HarmonyLib;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using IGameAdapter = CasualtiesUnknownOnline.Runtime.GameAdapter.IGameAdapter;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Game Adapter for the current Casualties Unknown (Demo) build (architecture.md
/// §4). The only layer that knows game types: it hooks input, freezes/simulates
/// player bodies, clones remote players and captures/applies world-start
/// parameters. The sync semantics live in the Runtime domain services
/// (SessionService / EntitySyncService / CharacterDataStore / WorldService);
/// this class only shuttles state between game objects and the domains.
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
	private readonly EntitySyncService _entities;
	private readonly CharacterDataStore _characterData;
	private readonly WorldService _world;
	private readonly ItemService _items;
	private readonly ILogger<GameAdapter> _log;
	private readonly IMapper _mapper;
	private Harmony? _harmony;

	private Body? _localBody;

	/// <summary>
	/// Host only: a deep copy of worldBlocks taken when generation completes —
	/// the generated baseline. The damage table diffs against it: a block whose
	/// current state equals its baseline entry is not a difference (restored),
	/// anything else is. Reset (re-captured) on every generation completion,
	/// i.e. per world/layer.
	/// </summary>
	private ushort[,]? _baseline;
	private readonly Dictionary<ulong, Body> _remoteClones = []; // member SteamId → render clone
	private bool _inWorld;
	private bool _worldJoinPending; // guest: the host's enter instruction arrived while the menu was still loading
	private bool _startRunAuthorized; // set right before the WorldJoin-triggered StartRun, consumed by the gate
	private bool _worldReadyReceived; // guest: the host released the start gate (or let us in as a late joiner)
	private bool _gateFrozen; // the start gate holds us: world timeScale=0 + movingAllowed locked — restore both on release
	private readonly List<Button> _blockedButtons = []; // guest-in-session: menu buttons that open the start screen / enter a world

	private const float CharacterReportInterval = 1f; // guest → host character snapshot (1 Hz)
	private long _nextCharacterReportMs;
	private CharacterDataMsg? _pendingRestore; // guest side: host-sent restore, applied once the body exists
	private bool _restoreWipePending; // first pass wiped the slots (Destroy is end-of-frame) — items go in on the next frame

	// ---- World items (runtime-generated item entities) ----
	private bool _applyingRemoteItem; // reentry guard: remote applications must not report back
	private bool _applyingRemoteBuildingDamage; // reentry guard: applying a remote entity hit must not re-report
	private bool _deferredLifePodSound; // the spawn landing sound, deferred while the start gate holds
	private bool _replayingLifePodSound; // true while the deferred sound is being replayed (bypasses the defer patch)
	private bool _deferredLifePodShake; // the spawn landing camera shake (Invoke runs on real time — it fires into the frozen wait and is lost)

	/// <summary>Periodic world-item keyframe: re-send the full table so physical drift self-heals.</summary>
	private const int ItemSnapshotIntervalMs = 5000;
	private long _nextItemSnapshotMs;
	private ulong _nextItemId = 1; // local instance-id counter — ids are (counter << 32 | account id), unique without host allocation
	private readonly Dictionary<ulong, Vector2> _pickupOrigins = []; // itemId → world position at pickup (rollback target for a refused pickup)

	public GameAdapter(SessionService session, EntitySyncService entities, CharacterDataStore characterData,
		WorldService world, ItemService items, ILogger<GameAdapter> log, IMapper mapper)
	{
		_session = session;
		_entities = entities;
		_characterData = characterData;
		_world = world;
		_items = items;
		_log = log;
		_mapper = mapper;
		PatchBridge.Bind(this); // the only static seam — Harmony patches read the narrow surface, never this instance
	}

	public string CapabilityReport { get; private set; } = "Not probed";

	/// <summary>Guest-side input interception active (in a live session as guest).</summary>
	internal bool IsGuestMode => _session.Role == SessionRole.Guest && _session.SessionActive;

	bool IPatchBridge.IsSessionActive => _session.SessionActive;

	bool IPatchBridge.IsReplayingLifePodSound => _replayingLifePodSound;

	/// <summary>
	/// World generation is ALWAYS wrapped with random-stream isolation
	/// (WorldGenRandomIsolation): solo or session, the generation stream
	/// advances purely from generation code. A solo-generated world is
	/// therefore reproducible — a guest joining later restores the captured
	/// Random.state (CaptureWorldParams runs for Role=None too) and generates
	/// the identical world, which is what makes mid-session joining work.
	/// </summary>
	bool IPatchBridge.IsWorldGenIsolated => true;

	/// <summary>Host in a live session: authoritative world mutations (damage table capture).</summary>
	internal bool IsHostMode => _session.Role == SessionRole.Host && _session.SessionActive;

	/// <summary>
	/// True while the start gate holds this player: host while it waits for the
	/// guests' InWorld (StartGateActive), guest while in-world and the host has
	/// not released the gate yet. Frozen (movingAllowed) + full-screen overlay.
	/// Solo (no session) never waits — there is no gate and no WorldReady.
	/// </summary>
	internal bool WaitingForReady => _session.Role switch
	{
		SessionRole.Host => _world.StartGateActive,
		SessionRole.Guest => _inWorld && !_worldReadyReceived,
		_ => false, // solo play — no session, no gate
	};

	bool IPatchBridge.IsWaitingForReady => WaitingForReady;

	bool IGameAdapter.IsWaitingForReady => WaitingForReady;

	/// <summary>
	/// Guest: when the host finished loading (its InWorld arrived via the
	/// SceneState relay) — the anchor for the guest-side 30 s countdown.
	/// </summary>
	private long _hostInWorldSinceMs;

	/// <summary>
	/// Overlay text while the gate holds: who we are waiting for and the
	/// force-start countdown. Host counts against the real gate (armed at its
	/// world entry); guest counts 30 s from the host's InWorld relay (network
	/// delay approximation of the host's own gate).
	/// </summary>
	string IGameAdapter.WaitingText
	{
		get
		{
			if (_session.Role == SessionRole.Host)
			{
				var waiting = _session.Members.Count(m => m.SteamId != _session.LocalSteamId && !m.InWorld);
				return $"Waiting for {waiting} player(s) to load… ({_world.StartGateRemainingMs / 1000}s)";
			}

			if (!_session.IsRemoteInWorld(_session.HostSteamId))
			{
				return "Waiting for the host to load…";
			}

			var others = _session.Members.Count(m => m.SteamId != _session.LocalSteamId && m.SteamId != _session.HostSteamId && !m.InWorld);
			if (others > 0)
			{
				var remaining = Math.Max(0, (int)(30 - (Environment.TickCount - _hostInWorldSinceMs) / 1000d));
				return $"Waiting for {others} player(s)… ({remaining}s)";
			}

			return "Starting…";
		}
	}

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

	public void CaptureWorldParams()
	{
		// Host side: a new world (or layer) is generating — the damage table
		// starts empty again; mutations during generation are the baseline.
		_world.ResetDamagedBlocks();

		// Host side: snapshot what defines a run before generation consumes the
		// RNG. The world-defining fields (biome override/depth, total traveled)
		// were dead on the wire until this step — now captured with the RNG state.
		var randomState = RandomStateSerializer.Serialize(Random.state);
		var runSettings = HarmonyTraverse.ReadRunSettings();
		var biomeOverride = (byte)HarmonyTraverse.ReadBiomeOverride();
		var biomeDepth = (byte)HarmonyTraverse.ReadBiomeDepth();
		var totalTraveled = HarmonyTraverse.ReadTotalTraveled();
		_world.PublishWorldParams(new WorldStartParams
		{
			RandomState = randomState,
			RunSettings = runSettings,
			BiomeOverride = biomeOverride,
			BiomeDepth = biomeDepth,
			TotalTraveled = totalTraveled,
			// LoadedRun: no backing game field (PreRunScript.LoadRun is the
			// save-load flow — Phase 3 saves scope) — stays false on the wire.
		});
		_log.LogInformation("Captured world params ({StateBytes} bytes, {SettingCount} settings, "
			+ "biome {Biome}/{Depth}, traveled {Traveled}).",
			randomState.Length, runSettings?.Count ?? 0, biomeOverride, biomeDepth, totalTraveled);
	}

	public void ApplyWorldParams(WorldStartParams parameters)
	{
		// Guest side: restore the host's RNG state + run settings + world-defining
		// fields so local world generation produces the same world
		// (docs/game-internals.md).
		Random.state = RandomStateSerializer.Deserialize(parameters.RandomState);
		if (parameters.RunSettings is not null)
		{
			HarmonyTraverse.WriteRunSettings(parameters.RunSettings);
		}

		HarmonyTraverse.WriteBiomeOverride(parameters.BiomeOverride);
		HarmonyTraverse.WriteBiomeDepth(parameters.BiomeDepth);
		HarmonyTraverse.WriteTotalTraveled(parameters.TotalTraveled);

		_log.LogInformation("Applied host world params ({StateBytes} bytes).", parameters.RandomState.Length);
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
		UpdateSceneState();
		UpdateGuestMenuState();
		if (_session.Role != SessionRole.Guest)
		{
			// Host or solo: capture the generated baseline so the damage table
			// can diff against it (a solo game that opens a lobby later still
			// needs the table relative to the seed world, not the current one).
			TryCaptureWorldBaseline();
		}

		// Periodic world-item keyframes (host): re-send the full table
		// (unreliable) so physical drift self-heals — settled items get their
		// positions re-aligned by the receivers' next reconcile. The table
		// entries are refreshed to the CURRENT item positions first — the
		// spawn-time positions would pull settled items back into the air
		// every tick. Guest-generated items keep the guest's settled reports
		// (RefreshWorldItemStates only refreshes our own — generator authority).
		if (IsHostMode && Environment.TickCount >= _nextItemSnapshotMs)
		{
			_nextItemSnapshotMs = Environment.TickCount + ItemSnapshotIntervalMs;
			RefreshWorldItemStates();
			_items.SendPeriodicItemSnapshot();
		}

		// Generator-side position authority (guest): items this side generated
		// report their settled position once, so the table and the host's
		// phantom align to the generator's physics.
		UpdateItemSettleReports();

		UpdateStartGate();
		if (_worldJoinPending)
		{
			TryStartWorldJoin();
		}

		// Publish local state even before entity sync activates — the host's
		// PlayerJoin carries the local position, which was (0,0) before sync
		// because publishing only ran after activation.
		if (_localBody != null) // Unity object — == (is null misses scene-reload-destroyed)
		{
			PublishBodyState(_localBody);
			ReportCharacterDataIfDue();
			TryApplyCharacterRestore();
		}

		if (!_entities.EntitySyncActive)
		{
			return;
		}

		// Both sides render remote clones from the reported states. NO remote-side
		// simulation anywhere — each player simulates only its own body.
		// Lazy per-member ensure: a roster join can arrive before the member's
		// world exists (the menu scene has no "Experiment" template), and members
		// can join mid-session — retrying every frame absorbs all ordering races.
		foreach (var remote in _entities.RemotePlayers)
		{
			if (!_session.IsRemoteInWorld(remote.SteamId))
			{
				continue; // in a menu/loading — no clone
			}

			// == null on Unity objects — a scene reload destroys the clone and
			// reference-comparison would miss it; retry creation next frame.
			if (!_remoteClones.TryGetValue(remote.SteamId, out var clone) || clone == null)
			{
				clone = RemoteBodyFactory.CreateRemoteBody(remote, AnchorFor(remote), _log);
				if (clone == null)
				{
					continue; // template unavailable — retry next frame
				}

				_remoteClones[remote.SteamId] = clone;
				_log.LogInformation("Remote body created for {SteamId}.", remote.SteamId);
				// Render its carried items from the latest snapshot (a fresh
				// report follows within 1 s at the latest).
				if (_cloneData.TryGetValue(remote.SteamId, out var data))
				{
					ApplyCloneInventory(clone, data);
				}
			}

			SessionStatePump.Apply(remote, clone);
		}

		LogClonePosition();
	}

	private Vector2 AnchorFor(PlayerEntity remote) =>
		_session.Role == SessionRole.Host
			? new Vector2(_session.GetRemoteSpawnPos(remote.SteamId).X, _session.GetRemoteSpawnPos(remote.SteamId).Y)
			: new Vector2(remote.Position.X, remote.Position.Y);

	private long _nextCloneLogMs;

	/// <summary>Periodic clone diagnostics (1 Hz) — where the remote proxies actually are.</summary>
	private void LogClonePosition()
	{
		var nowMs = Environment.TickCount;
		if (nowMs < _nextCloneLogMs)
		{
			return;
		}

		_nextCloneLogMs = nowMs + 1000;
		if (_remoteClones.Count == 0)
		{
			return;
		}

		// KeyValuePair has no Deconstruct on net48 — iterate entries explicitly.
		foreach (var entry in _remoteClones)
		{
			var steamId = entry.Key;
			var clone = entry.Value;
			// == null on the Unity clone: a scene reload destroys it and
			// reference-comparison (?.) would throw on access.
			var pos = clone != null ? clone.transform.position : Vector3.zero;
			var remote = _entities.GetRemotePlayer(steamId);
			var reported = remote is not null
				? new Vector2(remote.Position.X, remote.Position.Y)
				: Vector2.zero;
			_log.LogDebug("Clone {SteamId}: at ({PX:F1}, {PY:F1}), reported ({RX:F1}, {RY:F1}), active {Active}",
				steamId, pos.x, pos.y, reported.x, reported.y, clone != null && clone.gameObject.activeInHierarchy);
		}
	}

	void ICuoService.Stop() => Uninstall();

	void IDisposable.Dispose()
	{
		// == null on the Unity clones (is null would miss scene-reload-destroyed objects).
		foreach (var clone in _remoteClones.Values)
		{
			if (clone != null)
			{
				UnityEngine.Object.Destroy(clone.transform.parent.gameObject);
			}
		}

		_remoteClones.Clear();

		_entities.RemoteJoined -= OnRemoteJoined;
		_session.RemoteSceneChanged -= OnRemoteSceneChanged;
		_session.SessionEnded -= OnSessionEnded;
		_session.SessionActivated -= OnSessionActivated;
		_world.BlockDamagedReceived -= OnRemoteBlockDamaged;
		_world.WorldJoinReceived -= OnWorldJoin;
		_world.BlockStateReceived -= OnRemoteBlockState;
		_world.BlockPlacedReceived -= OnRemoteBlockPlaced;
		_world.WorldReadyReceived -= OnRemoteWorldReady;
		_characterData.CharacterDataReceived -= OnCharacterDataReceived;
		PatchBridge.Unbind(this);
	}

	// ---- Session wiring ----

	internal void BindToSession()
	{
		_entities.RemoteJoined += OnRemoteJoined;
		_session.RemoteSceneChanged += OnRemoteSceneChanged;
		_session.SessionEnded += OnSessionEnded;
		_session.SessionActivated += OnSessionActivated;
		_world.BlockDamagedReceived += OnRemoteBlockDamaged;
		_world.BuildingEntityDamagedReceived += OnRemoteBuildingEntityDamaged;
		_world.BuildingEntityOpenedReceived += OnRemoteBuildingEntityOpened;
		_world.WorldJoinReceived += OnWorldJoin;
		_world.BlockStateReceived += OnRemoteBlockState;
		_world.BlockPlacedReceived += OnRemoteBlockPlaced;
		_world.WorldReadyReceived += OnRemoteWorldReady;
		_items.ItemSpawned += OnRemoteItemSpawned;
		_items.ItemPickedUp += OnRemoteItemPickedUp;
		_items.ItemDropped += OnRemoteItemDropped;
		_items.ItemDestroyed += OnRemoteItemDestroyed;
		_items.ItemRejected += OnItemRejected;
		_items.ItemSettledReceived += OnRemoteItemSettled;
		_items.ItemSnapshotReceived += OnRemoteItemSnapshot;
		_characterData.CharacterDataReceived += OnCharacterDataReceived;
		_characterData.HostCharacterDataReceived += OnHostCharacterDataReceived;
	}

	/// <summary>
	/// Re-report the scene state (with position) once the handshake completes —
	/// if we entered the world before connecting, the earlier report was not
	/// sent (no session yet) and the host would spawn our clone at (0,0).
	/// </summary>
	private void OnSessionActivated()
	{
		if (!_inWorld || _localBody == null) // Unity object — ==
		{
			return;
		}

		var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
		var pos = new NetVector2(_localBody.transform.position.x, _localBody.transform.position.y);
		_session.ReportSceneState(SceneStateType.InWorld, sceneName, pos);
	}

	private void OnRemoteJoined(PlayerEntity remote) =>
		// Clone creation is handled by the per-frame lazy ensure in Update —
		// the roster join can arrive before the member's world exists (the menu
		// scene has no "Experiment" template), so event-driven creation would
		// race. Log only; the pump creates and the anchor for host/guest differs.
		_log.LogInformation("Remote joined (clone ensured by the Update pump): {SteamId}.", remote.SteamId);

	private void OnRemoteSceneChanged(ulong steamId, bool inWorld)
	{
		if (inWorld && steamId == _session.HostSteamId)
		{
			_hostInWorldSinceMs = Environment.TickCount; // guest countdown anchor: the host finished loading
		}

		if (!inWorld)
		{
			// The member left the world: destroy its render clone — it carries
			// no state (character data lives in the host's save store; the
			// entity buffer lives in EntitySyncService), and the Update pump
			// rebuilds it when the member re-enters. NOTE: == null on Unity
			// objects — a scene reload destroys the clone and reference
			// comparison (is null / ?.) would miss it.
			if (_remoteClones.TryGetValue(steamId, out var clone) && clone != null)
			{
				UnityEngine.Object.Destroy(clone.transform.parent.gameObject);
			}

			_remoteClones.Remove(steamId);

			// The host leaving the world ends the world itself (host
			// authority): a guest must not keep playing inside a world whose
			// owner is gone — pull it back to the main menu. Only when we are
			// actually in the world; after the load the normal UpdateSceneState
			// flow re-reports InMenu to the host.
			if (steamId == _session.HostSteamId && _session.Role == SessionRole.Guest)
			{
				_worldJoinPending = false; // a fresh "host entered" must re-arm the follow
				if (_inWorld && PlayerCamera.main != null) // Unity object — ==
				{
					_log.LogInformation("Host left the world — returning to main menu.");
					PlayerCamera.main.ToMainMenu();
				}
			}
		}

		_log.LogInformation(inWorld
			? "Remote entered the world — clone rebuilt on rejoin."
			: "Remote not in world (menu or disconnected) — clone destroyed.");
	}

	/// <summary>Guest side: the host told us to enter the world (WorldJoin). The
	/// menu may still be loading when it arrives (a right-click "Join Game"
	/// launches a fresh process) — wait for PreRunScript, then start the run.</summary>
	private void OnWorldJoin()
	{
		_worldJoinPending = true;
		TryStartWorldJoin();
	}

	private void TryStartWorldJoin()
	{
		if (!_worldJoinPending || _inWorld)
		{
			return;
		}

		if (HarmonyTraverse.IsGenerating())
		{
			// The host re-broadcast WorldJoin (its layer switch re-runs
			// GenerateWorld) while our own generation is running — we are
			// already loading. Our layer switch is our own ContinueRun, never
			// the host's instruction.
			_worldJoinPending = false;
			return;
		}

		if (PreRunScript.instance == null) // Unity object — == (menu still loading)
		{
			return;
		}

		_worldJoinPending = false;
		_startRunAuthorized = true; // the gate refuses unauthorised (manual) guest starts
									// A tutorial world is followed via StartTutorial (it sets the tutorial
									// flag and nulls runSettings itself, PreRunScript.cs:307-314); anything
									// else via StartRun. The params already arrived before WorldJoin, so
									// the biome tells us which world the host is generating.
		var tutorial = _world.WorldParams?.BiomeOverride == (byte)WorldGeneration.OverrideSceneType.Tutorial;
		_log.LogInformation("World join received — starting {Run} to follow.", tutorial ? "the tutorial" : "a run");
		if (tutorial)
		{
			PreRunScript.instance.StartTutorial();
		}
		else
		{
			PreRunScript.instance.StartRun();
		}
	}

	private void OnSessionEnded()
	{
		// == null on the Unity clones (is null would miss scene-reload-destroyed objects).
		foreach (var clone in _remoteClones.Values)
		{
			if (clone != null)
			{
				UnityEngine.Object.Destroy(clone.transform.parent.gameObject);
			}
		}

		_remoteClones.Clear();

		// The session is gone — the start gate can never release us (no
		// WorldReady will come). Restore the world and local movement now,
		// not on some later frame.
		_worldReadyReceived = true;
		if (_gateFrozen)
		{
			_gateFrozen = false;
			Time.timeScale = 1f;
			if (_localBody != null) // Unity object — ==
			{
				Traverse.Create(_localBody).Field("movingAllowed").SetValue(true);
			}
		}
	}

	// ---- Block damage sync (local compute, remote verify/sync) ----

	private bool _applyingRemoteBlockDamage;

	/// <summary>Reentry guard while applying a remote block placement (suppresses the SetBlock hook's own report/broadcast).</summary>
	private bool _applyingRemoteBlockPlace;

	/// <summary>
	/// Called from the DamageBlock patch after a LOCAL block damage was applied:
	/// report it so the peer applies the same damage at the same world position.
	/// </summary>
	void IPatchBridge.OnBlockDamaged(Vector2 pos, float dmg)
	{
		if (_applyingRemoteBlockDamage || !_session.SessionActive)
		{
			return;
		}

		_world.SendBlockDamaged(new NetVector2(pos.x, pos.y), dmg);
	}

	/// <summary>
	/// The peer damaged a block — apply it locally (remote verify/sync). The
	/// damage comes with ignoreLoot=true: DamageBlock rolls the block's drops
	/// itself (WorldGeneration.cs:751) on the LOCAL Random stream, and the
	/// damage is applied on BOTH sides (this stream) — the ATTACKER's side
	/// already rolled locally (local compute) and reported the items, so a
	/// remote application must not roll a second, independent drop. The
	/// attacker's own call is a local DamageBlock with ignoreLoot=false.
	/// </summary>
	private void OnRemoteBlockDamaged(NetVector2 pos, float dmg)
	{
		if (WorldGeneration.world == null) // Unity object — ==
		{
			return;
		}

		_applyingRemoteBlockDamage = true;
		try
		{
			WorldGeneration.world.DamageBlock(
				WorldGeneration.world.WorldToBlockPos(new Vector2(pos.X, pos.Y)), dmg, true, false, true);
		}
		finally
		{
			_applyingRemoteBlockDamage = false;
		}
	}

	/// <summary>
	/// Called from the Body.Attack patch after the local attack damaged a
	/// building entity (Body.cs:1946 — the only player-vs-entity damage write,
	/// which otherwise stays local and the peer's copy of the entity never
	/// loses health): report it, position-keyed (world entities are generated
	/// deterministically, so both sides have the same object at the same place).
	/// </summary>
	void IPatchBridge.OnBuildingEntityDamaged(BuildingEntity entity, float damage)
	{
		if (_applyingRemoteBuildingDamage || !_session.SessionActive)
		{
			return;
		}

		var pos = entity.transform.position;
		_world.SendBuildingEntityDamaged(new NetVector2(pos.x, pos.y), damage);
	}

	/// <summary>
	/// A player's attack damaged a building entity — apply the damage to the
	/// entity at the reported position. A death applied HERE (via this message)
	/// is a REMOTE death: the attacker's side rolls and reports the drops
	/// (local compute — the entity's health is written on both sides, so both
	/// reach zero; only the attacker rolls), so this side is marked with
	/// RemoteEntityDeath and BuildingEntityUpdatePatch suppresses the roll —
	/// it only removes the entity.
	/// </summary>
	private void OnRemoteBuildingEntityDamaged(NetVector2 pos, float damage)
	{
		_applyingRemoteBuildingDamage = true;
		try
		{
			var hit = Physics2D.OverlapPoint(new Vector2(pos.X, pos.Y));
			var entity = hit != null ? hit.GetComponent<BuildingEntity>() : null; // Unity object — ==
			if (entity != null)
			{
				entity.health -= damage;
				if (entity.health < 0.5f)
				{
					entity.gameObject.AddComponent<RemoteEntityDeath>();
				}
			}
			else
			{
				_log.LogWarning("Building entity damage at {Pos} — no entity there (moved or already gone).", pos);
			}
		}
		finally
		{
			_applyingRemoteBuildingDamage = false;
		}
	}

	/// <summary>
	/// Called from the Openable/lockpick/keypad patches after a lockable entity
	/// was opened locally (all three paths write health = 0 directly) — report
	/// it, position-keyed like the entity damage.
	/// </summary>
	void IPatchBridge.OnBuildingEntityOpened(BuildingEntity entity)
	{
		if (_applyingRemoteBuildingDamage || !_session.SessionActive)
		{
			return;
		}

		var pos = entity.transform.position;
		_world.SendBuildingEntityOpened(new NetVector2(pos.x, pos.y));
	}

	/// <summary>
	/// A lockable entity was opened — apply the open (health = 0) to the entity
	/// at the reported position. Like the damage path, a death applied here is
	/// REMOTE: the opener's side rolls and reports the drops, this side is
	/// marked and BuildingEntityUpdatePatch only removes the entity.
	/// </summary>
	private void OnRemoteBuildingEntityOpened(NetVector2 pos)
	{
		_applyingRemoteBuildingDamage = true;
		try
		{
			var hit = Physics2D.OverlapPoint(new Vector2(pos.X, pos.Y));
			var entity = hit != null ? hit.GetComponent<BuildingEntity>() : null; // Unity object — ==
			if (entity != null)
			{
				entity.health = 0f;
				entity.gameObject.AddComponent<RemoteEntityDeath>();
			}
			else
			{
				_log.LogWarning("Building entity open at {Pos} — no entity there (moved or already gone).", pos);
			}
		}
		finally
		{
			_applyingRemoteBuildingDamage = false;
		}
	}

	/// <summary>
	/// Called from the SetBlock patch after any world mutation (mining,
	/// placement, remote application). Host/solo: diff against the generated
	/// baseline (equal → removed from the difference table, otherwise
	/// upserted) and broadcast placements live. Guest: report local
	/// placements to the host — breaking SetBlock(0) is already covered by
	/// the BlockDamaged stream, only non-air writes are placements. Remote
	/// applications are guarded (they answer their own way); generation-time
	/// SetBlock calls are the baseline itself and excluded.
	/// </summary>
	void IPatchBridge.OnBlockSet(Vector2Int pos, ushort block)
	{
		if (_applyingRemoteBlockPlace || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		// Host OR solo: diff against the generated baseline (equal → removed
		// from the difference table, otherwise upserted). Solo tracking is
		// what lets a solo game that opens a lobby later hand its accumulated
		// world changes to a joining guest (the guest regenerates the seed
		// world and applies the table). Guests do not track — they only apply.
		if (_session.Role != SessionRole.Guest)
		{
			if (_baseline is null)
			{
				TryCaptureWorldBaseline(); // generation may have just completed this frame
				if (_baseline is null)
				{
					return; // still no baseline — nothing to diff against
				}
			}

			if (block == _baseline[pos.x, pos.y])
			{
				_world.RemoveBlockState(pos.x, pos.y); // restored to baseline — no longer a difference
			}
			else
			{
				_world.ReportBlockState(pos.x, pos.y, block);
			}
		}

		if (block != 0 && _session.SessionActive)
		{
			// A placement in a live session: the source applied it locally
			// (local compute) — host broadcasts it, guest reports it for
			// arbitration. Solo (no session) never sends.
			if (_session.Role == SessionRole.Host)
			{
				_world.BroadcastBlockPlaced(0, pos.x, pos.y, block);
			}
			else if (_session.Role == SessionRole.Guest)
			{
				_world.SendBlockPlacedReport(pos.x, pos.y, block);
			}
		}
	}

	/// <summary>
	/// A placement arrived: host arbitrates (the target must be air — the
	/// game's own placement condition, Item.cs) — then applies, records the
	/// difference and relays (source excluded); guest applies it directly.
	/// </summary>
	private void OnRemoteBlockPlaced(ulong sender, int x, int y, ushort block)
	{
		if (WorldGeneration.world == null) // Unity object — ==
		{
			return;
		}

		_applyingRemoteBlockPlace = true;
		try
		{
			var pos = new Vector2Int(x, y);
			if (IsHostMode)
			{
				if (WorldGeneration.world.GetBlock(pos) != 0)
				{
					_log.LogWarning("Rejected remote block placement at ({X},{Y}): target not air (arbitration — reject+correction tier pending).", x, y);
					return; // target already occupied — first-writer-wins, no relay
				}

				WorldGeneration.world.SetBlock(pos, block);
				_world.ReportBlockState(x, y, block); // the placement is a world difference too
				_world.BroadcastBlockPlaced(sender, x, y, block); // the reporter already placed locally
			}
			else
			{
				WorldGeneration.world.SetBlock(pos, block);
			}
		}
		finally
		{
			_applyingRemoteBlockPlace = false;
		}
	}

	// ---- World items (runtime-generated item entities, local compute → report → relay) ----

	/// <summary>Instance ids are (local counter, account id) — globally unique per
	/// session without host allocation, so the spawner applies its item
	/// immediately (local compute, zero pickup latency).</summary>
	private ulong NextItemId() => (_nextItemId++ << 32) | (uint)_session.LocalSteamId;

	/// <summary>
	/// Return the item's instance id, allocating one when it does not have it
	/// yet — a generation-time item (world-gen determinism covers it, no id)
	/// that enters the world domain through a runtime act (dropped from an
	/// inventory, unloaded from a container) needs an id so the peers can
	/// materialize it. Returns 0 when the item is not eligible (still
	/// generating).
	/// </summary>
	private ulong EnsureItemId(Item item)
	{
		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null) // Unity object — ==
		{
			return idComp.Id;
		}

		if (HarmonyTraverse.IsGenerating())
		{
			return 0; // generation-time instantiation — the world-gen determinism covers it
		}

		idComp = item.gameObject.AddComponent<ItemInstanceId>();
		idComp.Id = NextItemId();
		return idComp.Id;
	}

	/// <summary>True when the item's parent chain ends outside any inventory/body — it is part of the world.</summary>
	internal static bool IsWorldItem(Item item)
	{
		var t = item.transform;
		while (t != null)
		{
			// == null on Unity objects (a scene-reload-destroyed parent is not managed-null)
			if (t.GetComponent<InventorySlot>() != null || t.GetComponent<Body>() != null)
			{
				return false;
			}

			t = t.parent;
		}

		return true;
	}

	/// <summary>
	/// Guest only: items this side generated (instance-id low bits = local
	/// SteamId, see NextItemId) report their SETTLED position once — the
	/// authoritative table (and the host's phantom) align to the generator's
	/// physics instead of the receiver-side drift ("item fell through the
	/// world" / "pulled back to the host's spot" class of bugs). "Settled" is
	/// the rigidbody's own sleep state — a velocity threshold re-armed on
	/// every roll-and-stop cycle and re-sent the same item dozens of times,
	/// yanking the host's phantom around (observed in the settle log spam).
	/// Sleeping is a stable terminal state: report exactly once, re-arm when
	/// the body wakes (kicked/picked up).
	/// </summary>
	private long _nextSettleCheckMs;
	private readonly HashSet<ulong> _settledReported = [];

	private void UpdateItemSettleReports()
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		var nowMs = Environment.TickCount;
		if (nowMs < _nextSettleCheckMs)
		{
			return;
		}

		_nextSettleCheckMs = nowMs + 500;
		var me = (uint)_session.LocalSteamId;
		foreach (var item in Item.allItems)
		{
			var idComp = item.GetComponent<ItemInstanceId>();
			if (idComp == null || (uint)(idComp.Id & 0xFFFFFFFF) != me || !IsWorldItem(item)) // Unity object — ==
			{
				continue;
			}

			if (!item.rb.IsSleeping() || item.rb.velocity.magnitude > 0.1f)
			{
				_settledReported.Remove(idComp.Id); // awake or creeping — it may settle later
				continue;
			}

			if (_settledReported.Add(idComp.Id))
			{
				_items.SendItemSettle(idComp.Id,
					new NetVector2(item.transform.position.x, item.transform.position.y),
					item.transform.eulerAngles.z);
			}
		}
	}

	/// <summary>
	/// Host side: a guest's item settled — align the local phantom to the
	/// generator's position (zero velocity + sleep so the local physics does
	/// not push it back). The table entry was already updated by ItemService.
	/// </summary>
	private void OnRemoteItemSettled(ulong itemId, NetVector2 pos, float rotation)
	{
		_applyingRemoteItem = true;
		try
		{
			var item = FindWorldItem(itemId);
			if (item != null && item.rb != null) // Unity objects — ==
			{
				item.transform.position = new Vector3(pos.X, pos.Y, 0f);
				item.transform.eulerAngles = new Vector3(0f, 0f, rotation);
				item.rb.velocity = Vector2.zero;
				item.rb.angularVelocity = 0f;
				item.rb.Sleep();
			}
		}
		finally
		{
			_applyingRemoteItem = false;
		}
	}

	/// <summary>Host only: push the items' live state into the authoritative
	/// table before the periodic keyframe — the entries otherwise hold the
	/// spawn-time positions and the keyframe would yank settled items around.
	/// Only items the host itself generated are refreshed from the local
	/// physics: the guest's items are position-authoritative on the guest side
	/// (the instance id's low 32 bits are the generator's SteamId — see
	/// NextItemId), and their table entries keep the guest's settled reports
	/// (ItemSettle) instead of the host-side phantom's drift.</summary>
	private void RefreshWorldItemStates()
	{
		var me = (uint)_session.LocalSteamId;
		foreach (var item in Item.allItems)
		{
			var idComp = item.GetComponent<ItemInstanceId>();
			if (idComp == null || (uint)(idComp.Id & 0xFFFFFFFF) != me) // Unity object — ==; only our own items
			{
				continue;
			}

			_items.RefreshItemState(idComp.Id,
				new NetVector2(item.transform.position.x, item.transform.position.y),
				new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
				item.transform.eulerAngles.z);
		}
	}

	/// <summary>
	/// Find a generation-time (id-less) item of the same definition near Pos —
	/// the materialization bind target. Only items outside any inventory count
	/// (world-gen determinism put them there on every side).
	/// </summary>
	private static Item? FindExistingAt(NetVector2 pos, string itemId)
	{
		var target = new Vector2(pos.X, pos.Y);
		foreach (var item in Item.allItems)
		{
			if (item.id != itemId || !IsWorldItem(item)) // Unity object — ==
			{
				continue;
			}

			if (item.GetComponent<ItemInstanceId>() != null) // Unity object — ==; already an item-domain object
			{
				continue;
			}

			if (Vector2.Distance(item.transform.position, target) > 1.5f)
			{
				continue;
			}

			return item;
		}

		return null;
	}

	/// <summary>
	/// Find an item by its instance id. Item.allItems registers in Item.Start
	/// (Item.cs:118) — ONE frame after Instantiate — so a message arriving in
	/// the same frame as a materialization misses the table (observed: a pickup
	/// relay 3 ms after the drop left the world phantom forever — "moving an
	/// item makes a duplicate drop on the peer"). Fall back to a scene scan
	/// (slow, but only on the miss path) to cover the not-yet-registered window.
	/// </summary>
	private static Item? FindWorldItem(ulong itemId)
	{
		foreach (var item in Item.allItems)
		{
			var idComp = item.GetComponent<ItemInstanceId>();
			if (idComp != null && idComp.Id == itemId) // Unity object — ==
			{
				return item;
			}
		}

		foreach (var item in UnityEngine.Object.FindObjectsOfType<Item>())
		{
			var idComp = item.GetComponent<ItemInstanceId>();
			if (idComp != null && idComp.Id == itemId) // Unity object — ==
			{
				return item;
			}
		}

		return null;
	}

	/// <summary>
	/// Called from the Item.Start patch after a runtime-generated item appeared
	/// (drops, creature loot, use-spawned items — every instantiation lands
	/// here). Generation-time items are skipped (world-gen determinism covers
	/// them); everything else gets an instance id and is reported. Solo play
	/// records too (no broadcast) — a solo-turned-lobby host hands its
	/// accumulated items to a joining guest via the snapshot.
	/// An item that is already inside an inventory/container when Start runs is
	/// NOT a world item: the game's own flow instantiates and picks up in the
	/// same frame (the starting supplies, WorldGeneration.cs:1904-1912; use
	/// transforms like the empty bottle, Item.cs:1442) and MonoBehaviour.Start
	/// only fires on the NEXT frame — after generation finished, so the
	/// IsGenerating guard alone would misclassify them as runtime spawns and
	/// duplicate them for the peers.
	/// </summary>
	void IPatchBridge.OnItemInstantiated(Item item)
	{
		if (_applyingRemoteItem || HarmonyTraverse.IsGenerating() || !IsWorldItem(item))
		{
			return;
		}

		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null) // Unity object — ==; remote application attached it first — already synced
		{
			return;
		}

		idComp = item.gameObject.AddComponent<ItemInstanceId>();
		idComp.Id = NextItemId();
		// The glowing floating pickup effect carries over. Drops are executed
		// on the ATTACKER's side (local compute), so the game's 8 ft proximity
		// check (BuildingEntity.cs:74) already ran against the attacker's own
		// distance — the component on the object is the truth.
		var fresh = item.GetComponent<FreshItemDrop>() != null; // Unity object — ==
		_log.LogInformation("[ItemSpawned] local {Type} (id {ItemId}) reported at ({X:F1},{Y:F1}), vel ({VX:F1},{VY:F1}), fresh {Fresh}.",
			item.id, idComp.Id, item.transform.position.x, item.transform.position.y,
			item.rb.velocity.x, item.rb.velocity.y, fresh);
		_items.SendItemSpawned(idComp.Id, CaptureItem(item, -1),
			new NetVector2(item.transform.position.x, item.transform.position.y),
			new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
			item.transform.eulerAngles.z,
			fresh);
	}

	void IPatchBridge.OnItemDestroyed(Item item)
	{
		if (_applyingRemoteItem || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null && idComp.Id != 0) // Unity object — ==; remote deletions zero the id (see KillRemoteItem)
		{
			_items.SendItemDestroyed(idComp.Id);
		}
	}

	/// <summary>
	/// Remove an item object as a REMOTE application: zero its instance id
	/// immediately, then Destroy. UnityEngine.Object.Destroy is deferred to
	/// end-of-frame, so the OnDestroy hook fires AFTER the reentry guard has
	/// been restored — without zeroing the id first, every remote deletion
	/// would echo back as a local destroy report and kill the peer's own copy
	/// (observed: picking up an item destroyed it on the picker's side too).
	/// </summary>
	private static void KillRemoteItem(Item item)
	{
		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null) // Unity object — ==
		{
			idComp.Id = 0;
		}

		UnityEngine.Object.Destroy(item.gameObject);
	}

	void IPatchBridge.OnItemPickupStart(Item item)
	{
		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null && _pickupOrigins.Count < 256) // Unity object — ==; bounded, oldest overwritten
		{
			_pickupOrigins[idComp.Id] = item.transform.position;
		}
	}

	void IPatchBridge.OnItemPickedUp(Item item)
	{
		if (_applyingRemoteItem)
		{
			return;
		}

		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null) // Unity object — ==
		{
			_items.SendItemPickedUp(idComp.Id);
		}
	}

	void IPatchBridge.OnItemDropped(Item item)
	{
		if (_applyingRemoteItem)
		{
			return;
		}

		var itemId = EnsureItemId(item);
		if (itemId != 0)
		{
			_items.SendItemDropped(itemId, CaptureItem(item, -1),
				new NetVector2(item.transform.position.x, item.transform.position.y),
				new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
				0, item.transform.eulerAngles.z);
		}
	}

	void IPatchBridge.OnItemThrown(Item item)
	{
		if (_applyingRemoteItem)
		{
			return;
		}

		var itemId = EnsureItemId(item);
		if (itemId != 0)
		{
			// The throw velocity is set AFTER the drop report (Body.cs:1659-1661)
			// — this second report carries it; the peer's copy (already
			// materialized by the first) gets re-placed with the flight
			// velocity instead of dropping in place.
			_items.SendItemDropped(itemId, CaptureItem(item, -1),
				new NetVector2(item.transform.position.x, item.transform.position.y),
				new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
				0, item.transform.eulerAngles.z);
		}
	}

	void IPatchBridge.OnItemLoadedIntoContainer(Item item, bool wasWorldItem)
	{
		if (_applyingRemoteItem)
		{
			return;
		}

		var itemId = EnsureItemId(item);
		if (itemId == 0)
		{
			return;
		}

		if (!IsWorldItem(item))
		{
			// The item left the world into a BODY-side container (a backpack or
			// held container — dragging a ground item into the bag in your
			// inventory goes through LoadItem, NOT PickUpItem, so the world-item
			// copy would stay on the peer: "still on the ground"). World →
			// inventory is pickup semantics — report it.
			if (wasWorldItem)
			{
				_log.LogInformation("[ContainerLoad] {Type} (id {ItemId}) left the world into a body container — pickup report.", item.id, itemId);
				_items.SendItemPickedUp(itemId);
			}

			return;
		}

		// A WORLD container (a trash bag on the ground, generation-time — no
		// instance id) becomes an item-domain object on first use: it gets an
		// id here, and the item's drop message carries the container's position
		// so the peers can bind their local (also generation-time, id-less)
		// container by position and place the item inside it. A container that
		// just entered the domain is REGISTERED (spawn report): the peers bind
		// their local copy instead of materializing, and the table entry keeps
		// the snapshot reconcile from killing the bound local container.
		var containerItem = item.transform.parent != null ? item.transform.parent.GetComponent<Item>() : null;
		ulong containerId = 0;
		var parentPos = new NetVector2(0f, 0f);
		if (containerItem != null) // Unity object — ==; the container position always travels (the receiver binds a local generation-time container by position)
		{
			parentPos = new NetVector2(containerItem.transform.position.x, containerItem.transform.position.y);
			if (IsWorldItem(containerItem))
			{
				var containerIdComp = containerItem.GetComponent<ItemInstanceId>();
				if (containerIdComp == null) // Unity object — ==; first use of a generation-time container
				{
					containerId = EnsureItemId(containerItem);
					var containerPos = new NetVector2(containerItem.transform.position.x, containerItem.transform.position.y);
					_items.SendItemSpawned(containerId, CaptureItem(containerItem, -1), containerPos,
						new NetVector2(0f, 0f), containerItem.transform.eulerAngles.z, false);
				}
				else
				{
					containerId = containerIdComp.Id;
				}
			}
		}

		_log.LogInformation("[ContainerLoad] {Type} (id {ItemId}) into container {ContainerId} ({ContainerType}) at ({X:F1},{Y:F1}), parentPos ({PX:F1},{PY:F1}).",
			item.id, itemId, containerId, containerItem?.id ?? "none",
			item.transform.position.x, item.transform.position.y, parentPos.X, parentPos.Y);
		_items.SendItemDropped(itemId, CaptureItem(item, -1),
			new NetVector2(item.transform.position.x, item.transform.position.y),
			new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
			containerId, item.transform.eulerAngles.z, parentPos);
	}

	void IPatchBridge.OnItemUnloadedFromContainer(Item item)
	{
		if (_applyingRemoteItem)
		{
			return;
		}

		var itemId = EnsureItemId(item);
		if (itemId != 0)
		{
			_items.SendItemDropped(itemId, CaptureItem(item, -1),
				new NetVector2(item.transform.position.x, item.transform.position.y),
				new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
				0, item.transform.eulerAngles.z);
		}
	}

	void IPatchBridge.OnContainerUnloadedAll(Container container)
	{
		if (_applyingRemoteItem)
		{
			return;
		}

		for (var i = 0; i < container.transform.childCount; i++)
		{
			var child = container.transform.GetChild(i).GetComponent<Item>();
			if (child == null) // Unity object — ==
			{
				continue;
			}

			var itemId = EnsureItemId(child);
			if (itemId != 0)
			{
				_items.SendItemDropped(itemId, CaptureItem(child, -1),
					new NetVector2(child.transform.position.x, child.transform.position.y),
					new NetVector2(child.rb.velocity.x, child.rb.velocity.y),
					0, child.transform.eulerAngles.z);
			}
		}
	}

	/// <summary>A world item now exists on a remote side — materialize it locally (full state: condition + components + contents).</summary>
	private void OnRemoteItemSpawned(WorldItem worldItem)
	{
		_applyingRemoteItem = true;
		try
		{
			SpawnWorldItem(worldItem);
		}
		finally
		{
			_applyingRemoteItem = false;
		}
	}

	/// <summary>
	/// A world item left the world into someone's inventory. We never receive
	/// the broadcast of our own successful pickup (the source is excluded), so
	/// this is someone else taking it — remove our world copy (if we still
	/// have one). An item that is NOT a world item on this side (inside an
	/// inventory) is left untouched: this side owns that copy already, the
	/// peer took its own — rolling it back would yank the item out of this
	/// side's inventory ("a duplicate fell out of the hand" — the item id
	/// matched the local carried item via a leftover world phantom).
	/// A lost optimistic-pickup race rolls back through ItemReject, never here.
	/// </summary>
	private void OnRemoteItemPickedUp(ulong itemId)
	{
		_applyingRemoteItem = true;
		try
		{
			var item = FindWorldItem(itemId);
			if (item != null && IsWorldItem(item)) // Unity objects — ==
			{
				KillRemoteItem(item);
			}
		}
		finally
		{
			_applyingRemoteItem = false;
		}
	}

	private void OnRemoteItemDropped(ulong itemId, CharacterItemMsg itemState, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, NetVector2 parentPos)
	{
		_applyingRemoteItem = true;
		try
		{
			var item = FindWorldItem(itemId);
			if (item == null) // Unity object — ==; we never had it (it was in the dropper's inventory)
			{
				_log.LogInformation("[ItemDrop] {Type} (id {ItemId}) not present — materializing at ({X:F1},{Y:F1}), container {ContainerId}, parentPos ({PX:F1},{PY:F1}).",
					itemState.ItemId, itemId, pos.X, pos.Y, parentItemId, parentPos.X, parentPos.Y);
				SpawnWorldItem(new WorldItem(itemId, itemState, pos, vel, parentItemId, rotation, false, parentPos));
			}
			else
			{
				_log.LogInformation("[ItemDrop] {Type} (id {ItemId}) present — re-placing at ({X:F1},{Y:F1}), container {ContainerId}.",
					itemState.ItemId, itemId, pos.X, pos.Y, parentItemId);
				item.transform.SetParent(null);
				item.transform.position = new Vector3(pos.X, pos.Y, 0f);
				item.transform.eulerAngles = new Vector3(0f, 0f, rotation);
				item.rb.velocity = new Vector2(vel.X, vel.Y); // a throw: re-applied mid-flight
				item.rb.angularVelocity = 0f;
				if (parentItemId != 0)
				{
					BindToContainer(item, parentItemId, parentPos);
				}
			}
		}
		finally
		{
			_applyingRemoteItem = false;
		}
	}

	/// <summary>
	/// Place an item inside its world container: the container's instance id
	/// (parentItemId) was allocated by the ORIGINATOR (possibly a generation-time
	/// container on first use — trash bags have no id until then), so the local
	/// container may not have it yet — bind it by the carried position, mirroring
	/// the game's LoadItem semantics (position + physics off + visibility).
	/// </summary>
	private void BindToContainer(Item item, ulong parentItemId, NetVector2 parentPos)
	{
		var parent = FindWorldItem(parentItemId);
		if (parent != null && parent.GetComponent<Container>() != null) // Unity objects — ==
		{
			_log.LogInformation("[ItemBind] container {ContainerId} found — loading {Type} into it.", parentItemId, item.id);
			parent.GetComponent<Container>()?.LoadItem(item); // the game's own load semantics (position, physics, visibility)
			return;
		}

		// Generation-time container not bound yet — find it by position and
		// stamp the originator's id onto it (idempotent: already bound to a
		// different id → not ours, keep looking). The position tolerance is
		// generous: the container may have been nudged by physics since the
		// report; a lone unbound container of the same type is accepted as a
		// fallback.
		var candidates = UnityEngine.Object.FindObjectsOfType<Container>();
		foreach (var container in candidates)
		{
			var containerItem = container.GetComponent<Item>();
			if (containerItem == null) // Unity object — ==
			{
				continue;
			}

			if (Vector2.Distance(container.transform.position, new Vector2(parentPos.X, parentPos.Y)) > 3f)
			{
				continue;
			}

			var idComp = containerItem.GetComponent<ItemInstanceId>();
			if (idComp != null && idComp.Id != parentItemId) // Unity object — ==; bound to a different container
			{
				continue;
			}

			if (idComp == null) // Unity object — ==
			{
				idComp = containerItem.gameObject.AddComponent<ItemInstanceId>();
				idComp.Id = parentItemId;
			}

			_log.LogInformation("[ItemBind] container {ContainerId} bound by position ({X:F1},{Y:F1}) — loading {Type} into it.",
				parentItemId, parentPos.X, parentPos.Y, item.id);
			container.LoadItem(item);
			return;
		}

		// Fallback: a lone unbound container of the same definition anywhere —
		// the position report may be stale (the container moved after the
		// report was sent).
		foreach (var container in candidates)
		{
			var containerItem = container.GetComponent<Item>();
			if (containerItem == null || containerItem.id != item.id) // Unity object — ==
			{
				continue;
			}

			if (containerItem.GetComponent<ItemInstanceId>() != null) // Unity object — ==; already bound
			{
				continue;
			}

			containerItem.gameObject.AddComponent<ItemInstanceId>().Id = parentItemId;
			_log.LogInformation("[ItemBind] container {ContainerId} bound as the lone {Type} (stale position {X:F1},{Y:F1}).",
				parentItemId, item.id, parentPos.X, parentPos.Y);
			container.LoadItem(item);
			return;
		}

		_log.LogWarning("[ItemBind] container {ParentItemId} for {Type} not found at ({X:F1}, {Y:F1}) — item stays where it is.",
			parentItemId, item.id, parentPos.X, parentPos.Y);
	}

	private void OnRemoteItemDestroyed(ulong itemId)
	{
		_applyingRemoteItem = true;
		try
		{
			var item = FindWorldItem(itemId);
			if (item != null) // Unity object — ==
			{
				KillRemoteItem(item);
			}
		}
		finally
		{
			_applyingRemoteItem = false;
		}
	}

	/// <summary>The host refused our pickup — take the item back out of the inventory and put it back where it was picked up.</summary>
	private void OnItemRejected(ulong itemId)
	{
		_applyingRemoteItem = true;
		try
		{
			var item = FindWorldItem(itemId);
			if (item != null) // Unity object — ==
			{
				RollbackPickup(item, itemId);
			}
		}
		finally
		{
			_applyingRemoteItem = false;
		}
	}

	/// <summary>
	/// A refused pickup or a lost race — the item leaves the inventory back
	/// into the world, at the position it was picked up from.
	/// </summary>
	private void RollbackPickup(Item item, ulong itemId)
	{
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
		if (body != null && body.HoldingItem(item)) // Unity object — ==
		{
			body.DropItem(item);
		}
		else if (item.transform.parent != null)
		{
			item.transform.SetParent(null); // mid-drag or inside a container — free it
			item.rb.simulated = true;
		}

		if (_pickupOrigins.TryGetValue(itemId, out var origin))
		{
			item.transform.position = origin;
			_pickupOrigins.Remove(itemId);
		}
	}

	/// <summary>
	/// The authoritative world-item snapshot arrived (world entry): reconcile —
	/// destroy local world items missing from the snapshot, materialize the
	/// snapshot's items (world first, then container contents — the parent
	/// objects must exist).
	/// </summary>
	private void OnRemoteItemSnapshot(IReadOnlyList<WorldItem> items)
	{
		_applyingRemoteItem = true;
		var killed = 0;
		var spawned = 0;
		var aligned = 0;
		try
		{
			var snapshot = items.ToDictionary(w => w.ItemId);

			foreach (var item in Item.allItems.ToList()) // copy: destroying while iterating
			{
				var idComp = item.GetComponent<ItemInstanceId>();
				if (idComp == null || !IsWorldItem(item)) // Unity object — ==; inventory items are character data
				{
					continue;
				}

				if (!snapshot.ContainsKey(idComp.Id))
				{
					KillRemoteItem(item);
					killed++;
				}
			}

			foreach (var w in items.Where(w => w.ParentItemId == 0))
			{
				var item = FindWorldItem(w.ItemId);
				if (item == null) // Unity object — ==
				{
					SpawnWorldItem(w);
					spawned++;
				}
				else if (item.transform.parent == null && item.rb.IsSleeping()
					&& (Vector2.Distance(item.transform.position, new Vector2(w.Pos.X, w.Pos.Y)) > 0.5f
						|| Mathf.Abs(Mathf.DeltaAngle(item.transform.eulerAngles.z, w.Rotation)) > 2f))
				{
					// A SLEEPING item drifted from the authoritative state —
					// re-align both and put the body to sleep (the re-position
					// would otherwise be "corrected" back by the local physics,
					// restarting the yank-roll loop). Only sleeping bodies are
					// aligned: creeping items (0.05-0.1 velocity, awake) were
					// pulled every keyframe and visibly stepped/jumped together
					// ("items suddenly overlapping / clumping").
					item.transform.position = new Vector3(w.Pos.X, w.Pos.Y, 0f);
					item.transform.eulerAngles = new Vector3(0f, 0f, w.Rotation);
					item.rb.velocity = Vector2.zero;
					item.rb.angularVelocity = 0f;
					item.rb.Sleep();
					aligned++;
				}
			}

			foreach (var w in items.Where(w => w.ParentItemId != 0))
			{
				if (FindWorldItem(w.ItemId) == null) // Unity object — ==
				{
					SpawnWorldItem(w);
					spawned++;
				}
			}
		}
		finally
		{
			_applyingRemoteItem = false;
		}

		if (killed > 0 || spawned > 0 || aligned > 0)
		{
			_log.LogInformation("[Reconcile] {Count} items: killed {Killed}, spawned {Spawned}, aligned {Aligned}.",
				items.Count, killed, spawned, aligned);
		}
	}

	/// <summary>
	/// Materialize a world item from its carried state: instantiate the
	/// definition prefab, restore condition/components/liquids/contents, attach
	/// the instance id and place it (into its container when the parent exists).
	/// The Item.Start hook sees the already-attached id and does not re-report.
	/// </summary>
	private void SpawnWorldItem(WorldItem w)
	{
		// A generation-time object may already exist at this spot (world-gen
		// determinism puts the same objects on every side): bind the instance
		// id to it instead of materializing a duplicate — a second copy would
		// also be killed by the next snapshot reconcile (one table entry, two
		// scene objects) and a generation-time container that was already
		// bound must NOT be re-materialized either ("items overlapping").
		var existing = FindExistingAt(w.Pos, w.Item.ItemId);
		if (existing != null) // Unity object — ==
		{
			var existingId = existing.GetComponent<ItemInstanceId>();
			if (existingId == null || existingId.Id == w.ItemId) // Unity object — ==; ours or still unbound
			{
				if (existingId == null) // Unity object — ==
				{
					existingId = existing.gameObject.AddComponent<ItemInstanceId>();
					existingId.Id = w.ItemId;
					existing.condition = w.Item.Condition; // match the originator's carried state
					RestoreLiquids(existing, w.Item.Liquids);
					RestoreComponentStates(existing, w.Item.Components);
					if (w.FreshItemDrop)
					{
						existing.gameObject.AddComponent<FreshItemDrop>();
					}

					// Align the bound object to the reported state — the
					// generation-time object sits where the world-gen put it,
					// which may differ from the originator's current spot
					// ("item in the wrong place / overlapping" class of bugs).
					existing.transform.position = new Vector3(w.Pos.X, w.Pos.Y, 0f);
					existing.transform.eulerAngles = new Vector3(0f, 0f, w.Rotation);
					existing.rb.velocity = new Vector2(w.Vel.X, w.Vel.Y);

					_log.LogInformation("[ItemBind] bound existing {Type} at ({X:F1}, {Y:F1}) to id {ItemId} (no materialization).",
						w.Item.ItemId, w.Pos.X, w.Pos.Y, w.ItemId);
				}

				if (w.ParentItemId != 0)
				{
					BindToContainer(existing, w.ParentItemId, w.ParentPosition);
				}

				return;
			}
		}

		_log.LogInformation("[ItemSpawn] materializing {Type} (id {ItemId}) at ({X:F1},{Y:F1}), vel ({VX:F1},{VY:F1}), container {ContainerId}.",
			w.Item.ItemId, w.ItemId, w.Pos.X, w.Pos.Y, w.Vel.X, w.Vel.Y, w.ParentItemId);
		var prefab = Resources.Load(w.Item.ItemId);
		if (prefab == null) // Unity object — ==
		{
			_log.LogWarning("Cannot materialize item {ItemId}: definition '{Type}' not found.", w.ItemId, w.Item.ItemId);
			return;
		}

		var obj = UnityEngine.Object.Instantiate(prefab, new Vector3(w.Pos.X, w.Pos.Y, 0f), Quaternion.Euler(0f, 0f, w.Rotation)) as GameObject;
		var item = obj!.GetComponent<Item>(); // the definition prefab carries Item — Instantiate succeeded, so it exists
		item.condition = w.Item.Condition; // direct write, like the save restore (SaveSystem.cs:306) — SetCondition would drain water by ratio
		item.favourited = w.Item.Favourited;
		item.gameObject.AddComponent<ItemInstanceId>().Id = w.ItemId;
		RestoreLiquids(item, w.Item.Liquids);
		RestoreComponentStates(item, w.Item.Components);
		RestoreContents(item, w.Item.Contents);
		if (w.FreshItemDrop)
		{
			item.gameObject.AddComponent<FreshItemDrop>(); // the glowing floating pickup effect (self-destroys when the setting is off)
		}

		if (w.ParentItemId != 0)
		{
			BindToContainer(item, w.ParentItemId, w.ParentPosition);
		}

		item.rb.velocity = new Vector2(w.Vel.X, w.Vel.Y);
	}

	/// <summary>
	/// Start-gate pump: the host forces the gate after 30 s (slow loaders
	/// finish on their own); both sides freeze the local player's movement
	/// while the gate holds and restore it on release.
	/// </summary>
	private void UpdateStartGate()
	{
		if (IsHostMode && _world.StartGateActive)
		{
			_world.MaybeForceStartGate();
		}

		if (WaitingForReady)
		{
			if (!_gateFrozen)
			{
				// True pause: the world must not simulate behind the overlay
				// (earthquake timers, temperature/radiation, liquids, NPCs).
				// PauseHandler's Update/TogglePause are patched off while the
				// gate holds, so nothing restores the timescale.
				Time.timeScale = 0f;
				_gateFrozen = true;
			}

			if (_localBody != null) // Unity object — ==
			{
				Traverse.Create(_localBody).Field("movingAllowed").SetValue(false);
			}
		}
		else if (_gateFrozen)
		{
			_gateFrozen = false;
			Time.timeScale = 1f;
			if (_localBody != null) // Unity object — ==
			{
				Traverse.Create(_localBody).Field("movingAllowed").SetValue(true);
			}

			ReplayLifePodEffects();
		}
		else if (_deferredLifePodSound || _deferredLifePodShake)
		{
			// Deferred but never held by a gate: the 30 s force-start released
			// the host BEFORE this side finished loading, so WorldReady arrived
			// first and WaitingForReady never armed — the release branch never
			// ran. Replay now that we are playing.
			ReplayLifePodEffects();
		}
	}

	/// <summary>
	/// The spawn landing presentation (sound + camera shake, WorldGeneration.cs:
	/// 3671-3674) was deferred while the start gate held (both would play into
	/// the frozen world — the shake's Invoke even runs on real time and is lost
	/// before the release). Replay both together at release. The replay flag
	/// keeps the Sound.Play patch from deferring it a second time (the session
	/// is still active at release).
	/// </summary>
	private void ReplayLifePodEffects()
	{
		if (_deferredLifePodSound)
		{
			_deferredLifePodSound = false;
			_log.LogInformation("[Sound] deferred lifePodHit played.");
			if (PlayerCamera.main != null && PlayerCamera.main.body != null) // Unity objects — ==
			{
				_replayingLifePodSound = true;
				try
				{
					Sound.Play("lifePodHit", PlayerCamera.main.body.transform.position, true, false, null, 1f, 1f, false, false);
				}
				finally
				{
					_replayingLifePodSound = false;
				}
			}
		}

		if (_deferredLifePodShake)
		{
			_deferredLifePodShake = false;
			_log.LogInformation("[FX] deferred LifePodShake played.");
			if (PlayerCamera.main != null) // Unity object — ==
			{
				PlayerCamera.main.LifePodShake();
			}
		}
	}

	/// <summary>Deferred by the Sound.Play patch while the start gate holds (it would play into the frozen world).</summary>
	void IPatchBridge.DeferLifePodSound()
	{
		_log.LogInformation("[Sound] lifePodHit deferred (gate holds or timeScale 0).");
		_deferredLifePodSound = true;
	}

	/// <summary>Deferred by the LifePodShake patch while the start gate holds (the Invoke fires into the frozen wait).</summary>
	void IPatchBridge.DeferLifePodShake()
	{
		_log.LogInformation("[FX] LifePodShake deferred (gate holds).");
		_deferredLifePodShake = true;
	}

	/// <summary>Guest side: the host released the start gate — start playing.</summary>
	private void OnRemoteWorldReady()
	{
		_worldReadyReceived = true;
		_log.LogInformation("World ready — start playing.");
	}

	/// <summary>
	/// Host only: snapshot worldBlocks the moment generation completes (the
	/// generated baseline the difference table diffs against). Any generation
	/// start resets the flag; a completed generation re-captures — per
	/// world/layer, matching the table reset at CaptureWorldParams.
	/// </summary>
	private void TryCaptureWorldBaseline()
	{
		var world = WorldGeneration.world;
		if (world == null || HarmonyTraverse.IsGenerating()) // Unity object — ==
		{
			_baseline = null;
			return;
		}

		if (_baseline is not null)
		{
			return; // already captured for this generation
		}

		var blocks = HarmonyTraverse.ReadWorldBlocks(world);
		if (blocks is null)
		{
			return;
		}

		_baseline = (ushort[,])blocks.Clone();
		_world.ResetDamagedBlocks();
		_log.LogInformation("Captured world baseline ({Width}x{Height}) — the damage table now diffs against it.",
			_baseline.GetLength(0), _baseline.GetLength(1));
	}

	/// <summary>
	/// Guest side: the host's authoritative block-state snapshot — apply the
	/// accumulated mutations to our freshly generated world (the snapshot only
	/// arrives after our InWorld report, i.e. after generation finished).
	/// </summary>
	private void OnRemoteBlockState(IReadOnlyList<DamagedBlock> blocks)
	{
		if (WorldGeneration.world == null || HarmonyTraverse.IsGenerating()) // Unity object — ==
		{
			return;
		}

		foreach (var block in blocks)
		{
			WorldGeneration.world.SetBlock(new Vector2Int(block.X, block.Y), block.Block);
		}

		_log.LogInformation("Applied host block-state snapshot ({Count} blocks).", blocks.Count);
	}

	// ---- Character data (session-scoped save/restore + clone inventory rendering) ----

	/// <summary>SteamId → latest character snapshot: the remote clone's inventory rendering source.</summary>
	private readonly Dictionary<ulong, CharacterDataMsg> _cloneData = [];

	private void OnCharacterDataReceived(ulong sender, CharacterDataMsg data)
	{
		if (_session.Role == SessionRole.Host)
		{
			// A guest's 1 Hz report — render its clone's inventory (the slots
			// show what it is carrying; the new body renders on creation from
			// this cache).
			_cloneData[sender] = data;
			var hasClone = _remoteClones.TryGetValue(sender, out var clone) && clone != null; // Unity object — ==
			_log.LogInformation("[CloneRender] host: char data from {Sender} ({Count} items), clone exists: {HasClone}", sender, data.Items.Count, hasClone);
			if (clone != null) // Unity object — ==
			{
				ApplyCloneInventory(clone, data);
			}
			return;
		}

		// May arrive before the local body exists (still loading the run) —
		// apply once the game has spawned it (TryApplyCharacterRestore).
		_pendingRestore = data;
		_log.LogInformation("Received character restore ({Items} items).", data.Items.Count);
	}

	/// <summary>Guest side: the host's own 1 Hz snapshot — render its clone's inventory (never applied to the local body).</summary>
	private void OnHostCharacterDataReceived(CharacterDataMsg data)
	{
		_cloneData[_session.HostSteamId] = data;
		var hasClone = _remoteClones.TryGetValue(_session.HostSteamId, out var clone) && clone != null; // Unity object — ==
		_log.LogInformation("[CloneRender] guest: host char data ({Count} items), clone exists: {HasClone}", data.Items.Count, hasClone);
		if (clone != null) // Unity object — ==
		{
			ApplyCloneInventory(clone, data);
		}
	}

	/// <summary>
	/// Render a remote clone's inventory from its owner's character snapshot:
	/// each slot shows the carried item's prefab (pure display — physics off,
	/// non-interactive, no instance id). Diff per slot: keep matching items,
	/// swap changed ones, destroy the emptied.
	/// </summary>
	private void ApplyCloneInventory(Body clone, CharacterDataMsg data)
	{
		_log.LogInformation("[CloneRender] apply {Count} items to clone slots ({Slots} slots).", data.Items.Count, clone.slots.Length);
		foreach (var slot in clone.slots)
		{
			if (slot == null) // Unity object — ==
			{
				continue;
			}

			var wanted = data.Items.FirstOrDefault(x => x.SlotIndex == slot.slot);

			// Clear EVERY child, then materialize the wanted item: the diff
			// used to inspect only GetChild(0), so a slot that accumulated
			// more than one child (template leftover + render, or repeated
			// renders) kept the strays — peers saw duplicate carried items
			// appear after inventory shuffling.
			for (var c = slot.transform.childCount - 1; c >= 0; c--)
			{
				UnityEngine.Object.Destroy(slot.transform.GetChild(c).gameObject);
			}

			if (wanted is null)
			{
				continue;
			}

			var prefab = Resources.Load(wanted.ItemId);
			if (prefab == null) // Unity object — ==
			{
				continue;
			}

			var obj = UnityEngine.Object.Instantiate(prefab, slot.transform) as GameObject;
			obj!.transform.localPosition = Vector3.zero;
			var item = obj.GetComponent<Item>();
			obj.transform.localEulerAngles = new Vector3(0f, 0f, item.Stats.slotRotation);
			if (item.rb != null) // Unity object — ==
			{
				item.rb.simulated = false; // pure display
			}

			var col = obj.GetComponent<Collider2D>();
			if (col != null) // Unity object — ==
			{
				col.enabled = false; // never pickable/blocking
			}

			var sr = obj.GetComponent<SpriteRenderer>();
			if (sr != null) // Unity object — ==
			{
				sr.sortingOrder = slot.spriteSortOrder;
			}
		}
	}

	private void ReportCharacterDataIfDue()
	{
		if (_pendingRestore is not null || _restoreWipePending)
		{
			return; // restoring: a fresh-run snapshot would overwrite the host's saved character data
		}

		var nowMs = Environment.TickCount;
		if (nowMs < _nextCharacterReportMs)
		{
			return;
		}

		_nextCharacterReportMs = nowMs + (long)(CharacterReportInterval * 1000f);
		var data = CaptureCharacterData(_localBody!);
		if (_session.Role == SessionRole.Host)
		{
			// Host → guests: their clones of the host render its carried items.
			_log.LogInformation("[CloneRender] host broadcasting char data ({Count} items).", data.Items.Count);
			_characterData.BroadcastHostCharacterData(data);
		}
		else
		{
			_log.LogInformation("[CloneRender] guest reporting char data ({Count} items).", data.Items.Count);
			_characterData.ReportCharacterData(data);
		}
	}

	private void TryApplyCharacterRestore()
	{
		if (_pendingRestore is null)
		{
			return;
		}

		// Apply only once world generation finished: the game hands out the
		// starting supplies inside generation (WorldPlacePlayer), and the
		// restore wipes the slots first — applying during generation would
		// race that handout (observed: the default lantern ending up on the
		// ground instead of in the restored inventory).
		if (HarmonyTraverse.IsGenerating())
		{
			return;
		}

		if (_restoreWipePending)
		{
			// Second pass (next frame): the wipe's Destroy ran at the end of
			// the previous frame, so the slots are actually empty now and
			// PickUpItem succeeds — it silently refuses a non-empty slot
			// (Body.cs:1388), which stranded the restored items on the ground.
			ApplyRestoredItems(_localBody!, _pendingRestore);
			_pendingRestore = null;
			_restoreWipePending = false;
			return;
		}

		ApplyRestoredStatsAndWipe(_localBody!, _pendingRestore);
		_restoreWipePending = true;
	}

	private CharacterDataMsg CaptureCharacterData(Body body)
	{
		var msg = new CharacterDataMsg
		{
			Skills = _mapper.Map<CharacterSkillsMsg>(body.skills),
			Health = _mapper.Map<CharacterHealthMsg>(body),
			HandSlot = body.handSlot,
		};

		// Limb has no Index field — Mapster maps the rest, the loop assigns it.
		for (var i = 0; i < body.limbs.Length; i++)
		{
			var limbMsg = _mapper.Map<CharacterLimbMsg>(body.limbs[i]);
			limbMsg.Index = i;
			msg.Limbs.Add(limbMsg);
		}

		// Items: id ↔ ItemId is a rename, not a case variant — keep it manual.
		// Capture is recursive: container contents ride inside the parent item
		// (Contents), and [Saveable] component state (liquids, batteries, ammo,
		// …) rides along — the wire form of the official save's SavedItem +
		// component dictionaries (SaveSystem.SaveGame), so a restore is complete.
		for (var slot = 0; slot < body.slots.Length; slot++)
		{
			var item = body.GetItem(slot);
			if (item == null) // Unity object — ==
			{
				continue;
			}

			msg.Items.Add(CaptureItem(item, slot));
		}

		return msg;
	}

	private void ApplyRestoredStatsAndWipe(Body body, CharacterDataMsg data)
	{
		_log.LogInformation("Applying character restore ({Items} items).", data.Items.Count);

		// Wipe the fresh-run default state first: this new run already got its
		// starting supplies (WorldGeneration.WorldPlacePlayer) and random vitals
		// (Body.Start) — restoring on top would duplicate items and leave
		// random hunger/thirst. Destroy is end-of-frame; the items are re-added
		// on the next frame (TryApplyCharacterRestore's second pass), so the
		// slots are actually empty when PickUpItem runs — it silently refuses
		// a non-empty slot (Body.cs:1388) and the item would be stranded.
		for (var slot = 0; slot < body.slots.Length; slot++)
		{
			var holder = body.slots[slot].transform;
			for (var i = holder.childCount - 1; i >= 0; i--)
			{
				UnityEngine.Object.Destroy(holder.GetChild(i).gameObject);
			}
		}

		if (data.Skills is { } skills)
		{
			_mapper.Map(skills, body.skills);
			body.skills.UpdateExpBoundaries(); // min/max derive from STR/RES/INT (Skills.cs:61)
		}

		if (data.Health is { } health)
		{
			// Target-driven: only writable Body members that exist in the source
			// are touched — alive/conscious (derived properties, Body.cs:203/213)
			// are read-only and skipped automatically.
			_mapper.Map(health, body);
		}

		foreach (var limbData in data.Limbs)
		{
			if (limbData.Index < 0 || limbData.Index >= body.limbs.Length)
			{
				continue;
			}

			_mapper.Map(limbData, body.limbs[limbData.Index]);
		}
	}

	private void ApplyRestoredItems(Body body, CharacterDataMsg data)
	{
		foreach (var itemData in data.Items)
		{
			RestoreItem(itemData, body);
		}

		if (data.HandSlot >= 0 && data.HandSlot < body.slots.Length)
		{
			body.handSlot = data.HandSlot;
		}
	}

	// ---- Item capture/restore (complete state: SavedItem fields + [Saveable] components + container contents) ----

	/// <summary>Recursively captures one item: the SavedItem fields (condition/
	/// favourited/slot), the WaterContainerItem liquid stacks, the [Saveable]
	/// component states and the container contents.</summary>
	private CharacterItemMsg CaptureItem(Item item, int slotIndex)
	{
		var msg = new CharacterItemMsg
		{
			ItemId = item.id,
			Condition = item.condition,
			SlotIndex = slotIndex,
			Favourited = item.favourited,
			Liquids = CaptureLiquids(item),
			Components = CaptureSaveableComponents(item),
		};

		var container = item.GetComponent<Container>();
		if (container != null) // Unity object — ==
		{
			for (var i = 0; i < container.transform.childCount; i++)
			{
				var child = container.transform.GetChild(i).GetComponent<Item>();
				if (child != null) // Unity object — ==
				{
					msg.Contents.Add(CaptureItem(child, slotIndex));
				}
			}
		}

		return msg;
	}

	/// <summary>The WaterContainerItem's liquid stacks — a public field
	/// (WaterContainerItem.cs:347), read directly for the round-trip symmetry
	/// with the restore (a game rename is a compile error, not a silent drop).</summary>
	private List<LiquidStackMsg> CaptureLiquids(Item item)
	{
		var water = item.GetComponent<WaterContainerItem>();
		if (water == null) // Unity object — ==
		{
			return [];
		}

		return [.. water.stack.Select(s => new LiquidStackMsg
		{
			LiquidId = s.liquidId,
			Amount = s.amount,
		})];
	}

	/// <summary>Snapshots every [Saveable] component's simple-typed state —
	/// the wire form of the official save's per-item component dictionaries.
	/// Unity-reference fields are never serialized; WaterContainerItem is
	/// skipped (its state travels as Liquids).</summary>
	private List<ComponentStateMsg> CaptureSaveableComponents(Item item)
	{
		var states = new List<ComponentStateMsg>();
		foreach (var comp in item.GetComponents<Component>())
		{
			if (comp is WaterContainerItem) // Unity object — ==
			{
				continue; // handled by CaptureLiquids
			}

			if (comp.GetType().GetCustomAttribute<Saveable>(inherit: false) is null)
			{
				continue;
			}

			var fields = new List<ComponentFieldMsg>();
			foreach (var field in comp.GetType().GetFields(
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
			{
				if (field.IsStatic || field.IsInitOnly)
				{
					continue;
				}

				// Private state must be explicitly marked for serialization
				// (the Unity serializer's rule, which the game relies on).
				if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() is null)
				{
					continue;
				}

				var kind = ComponentFieldKind(field.FieldType);
				if (kind == 0)
				{
					continue; // unsupported kind (Unity references, custom types)
				}

				var value = field.GetValue(comp);
				fields.Add(new ComponentFieldMsg
				{
					Name = field.Name,
					Kind = kind,
					FloatValue = kind == 1 ? (float)value! : 0f,
					IntValue = kind == 2 ? (int)value! : 0,
					BoolValue = kind == 3 && (bool)value!,
					StringValue = kind == 4 ? (string)value! : "",
					StringList = kind == 5 ? (List<string>)value! : [],
				});
			}

			states.Add(new ComponentStateMsg { TypeName = comp.GetType().Name, Fields = fields });
		}

		return states;
	}

	private static int ComponentFieldKind(Type type)
	{
		if (type == typeof(float))
		{
			return 1;
		}

		if (type == typeof(int))
		{
			return 2;
		}

		if (type == typeof(bool))
		{
			return 3;
		}

		if (type == typeof(string))
		{
			return 4;
		}

		if (type == typeof(List<string>))
		{
			return 5;
		}

		return 0;
	}

	/// <summary>Restores one item (recursively): instantiate by id, apply the
	/// SavedItem fields, the liquid stacks, the component states and the
	/// container contents, then hand it to the slot — with the game's own
	/// restore semantics (SaveSystem.cs:304-329): a non-empty slot takes the
	/// item into its container instead of failing.</summary>
	private void RestoreItem(CharacterItemMsg itemData, Body body)
	{
		if (itemData.SlotIndex < 0 || itemData.SlotIndex >= body.slots.Length)
		{
			return;
		}

		var go = UnityEngine.Object.Instantiate((GameObject)Resources.Load(itemData.ItemId),
			body.transform.position, Quaternion.identity);
		var item = go.GetComponent<Item>();
		if (item == null) // Unity object — ==
		{
			UnityEngine.Object.Destroy(go);
			_log.LogWarning("Restore: {ItemId} has no Item component — skipped.", itemData.ItemId);
			return;
		}

		item.condition = itemData.Condition;
		item.favourited = itemData.Favourited;
		RestoreLiquids(item, itemData.Liquids);
		RestoreComponentStates(item, itemData.Components);
		RestoreContents(item, itemData.Contents);

		if (body.HoldingItem(itemData.SlotIndex))
		{
			// The slot already holds something (a restored container) — the
			// item goes inside it (SaveSystem semantics, Body.cs:1388 would
			// silently refuse the slot otherwise).
			body.GetItem(itemData.SlotIndex).GetComponent<Container>()?.LoadItem(item);
		}
		else
		{
			body.PickUpItem(item, itemData.SlotIndex, force: true);
		}
	}

	private void RestoreLiquids(Item item, List<LiquidStackMsg> liquids)
	{
		var water = item.GetComponent<WaterContainerItem>();
		if (water == null) // Unity object — ==
		{
			return;
		}

		// Rebuild the stack directly instead of AddLiquid-ing: the prefab's
		// Awake already filled the default contents (WaterContainerItem.Awake),
		// so an additive restore reads "full" again. The capture side reads the
		// same public field, so this round-trips exactly (including an empty
		// stack).
		water.stack = [.. liquids.Select(l => new LiquidStack(l.LiquidId, l.Amount))];
	}

	private void RestoreComponentStates(Item item, List<ComponentStateMsg> states)
	{
		foreach (var state in states)
		{
			// Matched by type name: the capture side stores the component's
			// simple name, restore finds the component with that name.
			var comp = item.GetComponents<Component>()
				.FirstOrDefault(c => c.GetType().Name == state.TypeName);
			if (comp == null) // Unity object — == (FirstOrDefault on destroyed)
			{
				continue;
			}

			foreach (var field in state.Fields)
			{
				var target = comp.GetType().GetField(field.Name,
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				if (target is null || target.IsStatic || target.IsInitOnly)
				{
					continue;
				}

				switch (field.Kind)
				{
					case 1:
						target.SetValue(comp, field.FloatValue);
						break;
					case 2:
						target.SetValue(comp, field.IntValue);
						break;
					case 3:
						target.SetValue(comp, field.BoolValue);
						break;
					case 4:
						target.SetValue(comp, field.StringValue);
						break;
					case 5:
						target.SetValue(comp, field.StringList);
						break;
				}
			}
		}
	}

	private void RestoreContents(Item containerItem, List<CharacterItemMsg> contents)
	{
		if (contents.Count == 0)
		{
			return;
		}

		var container = containerItem.GetComponent<Container>();
		if (container == null) // Unity object — ==
		{
			return;
		}

		foreach (var childData in contents)
		{
			var go = UnityEngine.Object.Instantiate((GameObject)Resources.Load(childData.ItemId),
				containerItem.transform.position, Quaternion.identity);
			var child = go.GetComponent<Item>();
			if (child == null) // Unity object — ==
			{
				UnityEngine.Object.Destroy(go);
				_log.LogWarning("Restore: {ItemId} has no Item component — skipped.", childData.ItemId);
				continue;
			}

			child.condition = childData.Condition;
			child.favourited = childData.Favourited;
			RestoreLiquids(child, childData.Liquids);
			RestoreComponentStates(child, childData.Components);
			RestoreContents(child, childData.Contents);
			container.LoadItem(child);
		}
	}

	// ---- Scene state ----

	private void UpdateSceneState()
	{
		// == null on Unity singletons (is null misses scene-reload-destroyed objects).
		var inWorld = PlayerCamera.main != null && WorldGeneration.world != null
			&& !HarmonyTraverse.IsGenerating();
		if (inWorld == _inWorld)
		{
			if (inWorld && _localBody == null) // Unity object — ==
			{
				_localBody = PlayerCamera.main!.body;
			}

			return;
		}

		_inWorld = inWorld;
		var prevBody = _localBody; // Unity object — ==
		_localBody = inWorld ? PlayerCamera.main!.body : null;
		if (!inWorld && prevBody != null && _pendingRestore is null && !_restoreWipePending)
		{
			// Leaving the world (death, menu) — push a final snapshot so the
			// host's save carries the state at the moment of leaving, not the
			// last 1 Hz report (a death → re-enter cycle would otherwise
			// restore the pre-death state).
			_characterData.ReportCharacterData(CaptureCharacterData(prevBody));
		}

		if (inWorld && _session.Role == SessionRole.Host && _world.StartStartGate())
		{
			_log.LogInformation("Host entered the world — waiting for members before everyone starts.");
		}

		var sceneName = inWorld ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name : "PreGen";
		var pos = inWorld && _localBody != null // Unity object — ==
			? new NetVector2(_localBody.transform.position.x, _localBody.transform.position.y)
			: (NetVector2?)null;
		_session.ReportSceneState(inWorld ? SceneStateType.InWorld : SceneStateType.InMenu, sceneName, pos);
	}

	// ---- State shuttling ----

	private void PublishBodyState(Body body)
	{
		var pos = body.transform.position;
		var look = body.targetLookPos;
		var vel = body.rb.velocity;
		// Pose flags mirror the game's own pose rules:
		// - sitting: idle sit condition (Body.cs:3162), minus movingAllowed
		//   (private; sleeping is covered by the sleeping flag).
		// - sleeping: Body.cs:3961.
		// - climbing: currentClimbable (Body.cs:470).
		var sitting = body.idleTime > 12f && !body.exercising;
		_entities.PublishLocalState(
			new NetVector2(pos.x, pos.y),
			new NetVector2(look.x, look.y),
			new NetVector2(vel.x, vel.y),
			body.isRight, body.standing, body.alive, body.conscious, body.crouching,
			sitting, body.sleeping, body.currentClimbable != null); // Unity object — ==
	}

	/// <summary>Gate for every run-start entry (StartRun/LoadRun/StartTutorial) —
	/// returns false to block. A guest may only enter the world on the host's
	/// instruction (WorldJoin): starting on its own would create a world the
	/// host does not know. The lock starts as soon as the guest joined a lobby
	/// (HostSteamId set — before the handshake completes, so the Join-Game
	/// wait window is covered) and lifts when the lobby binding is gone. The
	/// WorldJoin path authorises its StartRun call right before it;
	/// LoadRun/StartTutorial have no authorised path.</summary>
	bool IPatchBridge.OnGuestStartAttempt()
	{
		if (_startRunAuthorized)
		{
			_startRunAuthorized = false;
			return true;
		}

		if (_session.Role == SessionRole.Guest && _session.HostSteamId != 0)
		{
			_log.LogWarning("A guest cannot start a run on its own — wait for the host to enter the world.");
			return false;
		}

		return true;
	}

	/// <summary>
	/// Guest side, bound to a lobby: the start screen is host-only. The menu's
	/// AdaptiveButtons (Play opens runSettingsScreen, Tutorial enters a world —
	/// AdaptiveButton.cs, not UnityEngine.UI.Button) are disabled by disabling
	/// the component (no click handling, no flash); the UnityEngine.UI.Button
	/// entries are disabled via interactable, and forcing the screen closed
	/// every frame stays as a backstop for any non-button open path.
	/// </summary>
	private void UpdateGuestMenuState()
	{
		var blocking = _session.Role == SessionRole.Guest && _session.HostSteamId != 0;

		foreach (var ab in UnityEngine.Object.FindObjectsOfType<AdaptiveButton>())
		{
			if (ab == null) // Unity object — ==
			{
				continue;
			}

			if (ab.action is AdaptiveButton.MenuAction.Play or AdaptiveButton.MenuAction.Tutorial)
			{
				if (ab.enabled == blocking)
				{
					ab.enabled = !blocking;
				}
			}
		}

		if (!blocking)
		{
			// Lobby binding gone — restore anything we disabled (the menu may
			// be reused for solo play) and drop the scan cache.
			foreach (var btn in _blockedButtons)
			{
				if (btn != null && !btn.interactable) // Unity object — ==
				{
					btn.interactable = true;
				}
			}

			_blockedButtons.Clear();
			return;
		}

		var pre = PreRunScript.instance;
		if (pre == null) // Unity object — == (menu not loaded)
		{
			return;
		}

		EnsureBlockedButtons(pre);
		foreach (var btn in _blockedButtons)
		{
			if (btn != null && btn.interactable) // Unity object — ==
			{
				btn.interactable = false;
			}
		}

		if (pre.runSettingsScreen != null && pre.runSettingsScreen.activeSelf) // Unity object — ==
		{
			pre.runSettingsScreen.SetActive(false); // backstop: any non-button open path
		}
	}

	private void EnsureBlockedButtons(PreRunScript pre)
	{
		// Cache validity: the menu scene rebuilds the buttons on reload — the
		// cached list is dead once every entry is a destroyed object.
		if (_blockedButtons.Count > 0 && _blockedButtons.Any(b => b != null))
		{
			return;
		}

		_blockedButtons.Clear();
		foreach (var btn in UnityEngine.Object.FindObjectsOfType<Button>())
		{
			if (btn == null) // Unity object — ==
			{
				continue;
			}

			for (var i = 0; i < btn.onClick.GetPersistentEventCount(); i++)
			{
				var target = btn.onClick.GetPersistentTarget(i);
				var method = btn.onClick.GetPersistentMethodName(i);
				// The button that opens the start screen (scene-wired
				// SetActive on runSettingsScreen) and the world entries.
				if ((target is GameObject go && go == pre.runSettingsScreen)
					|| (target is PreRunScript && method is "StartRun" or "LoadRun" or "StartTutorial"))
				{
					_blockedButtons.Add(btn);
					break;
				}
			}
		}
	}

	/// <summary>
	/// Called at the WorldGeneration.GenerateWorld boundary — the true start of
	/// generation. Host captures its RNG state (and any randomness consumed
	/// before this point is baked in); guest restores the host's state so both
	/// generate the identical world.
	/// </summary>
	void IPatchBridge.OnWorldGenerate()
	{
		if (_session.Role == SessionRole.Host || _session.Role == SessionRole.None)
		{
			CaptureWorldParams();
			// A new world layer is generating — the old layer's world items are
			// gone with the scene; the authoritative table starts empty again.
			_items.ResetItems();
		}
		else if (_world.WorldParams is not null)
		{
			ApplyWorldParams(_world.WorldParams);
		}
		else
		{
			_log.LogWarning("World generation started without host world params — world will not match!");
		}
	}

	/// <summary>Host clicked start — tell the members to start following IMMEDIATELY (their transition + loading begin together with ours, not one animation late). WorldJoin used to fire at generation start, which is a transition-animation late.</summary>
	void IPatchBridge.OnWorldJoinRequested()
	{
		if (_session.Role == SessionRole.Host && _session.SessionActive)
		{
			_log.LogInformation("[WorldJoin] sent at run-start entry — guests follow immediately.");
			_world.SendWorldJoin();
		}
	}

	/// <summary>An inventory-internal move (SwapSlots/SwitchHands) finished — re-report the character snapshot right away (the 1 Hz throttle alone reads as a 1-2 s delay on the peer's clone).</summary>
	void IPatchBridge.OnInventoryChanged()
	{
		if (_localBody != null && _session.SessionActive && _pendingRestore is null && !_restoreWipePending) // Unity object — ==
		{
			_log.LogInformation("[CloneRender] inventory changed — immediate re-report.");
			ForceReportCharacterData();
		}
	}

	private void ForceReportCharacterData()
	{
		var data = CaptureCharacterData(_localBody!);
		if (_session.Role == SessionRole.Host)
		{
			_characterData.BroadcastHostCharacterData(data);
		}
		else
		{
			_characterData.ReportCharacterData(data);
		}
	}

	public void Dispose() => throw new NotImplementedException();
}
