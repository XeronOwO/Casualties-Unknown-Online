using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.World;
using Microsoft.Extensions.Logging;

using System;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// Run-lifecycle domain: entering a world (host click → params/WorldJoin;
/// guest follow via WorldJoin), the world-start parameters (capture at the
/// click moment / GenerateWorld boundary, apply on the guest, the generation
/// stream baseline), scene state and the guest menu lock (host-only start
/// screen). The guest's follow is an explicit phase machine — the scattered
/// booleans that used to express it are the state's natural shape. The gate's
/// PRESENTATION (freeze/overlay/loading screen) lives in StartGateCoordinator,
/// which reads this machine's state.
/// </summary>
internal sealed class RunCoordinator(
	ISessionControl session,
	IWorldControl world,
	IEntitySyncControl entities,
	CharacterDataSync characterData,
	GuestMenuGuard guestMenu,
	WorldParamsService worldParams,
	ItemArbitration arbitration,
	ILogger<RunCoordinator> log)
{
	/// <summary>Guest run-follow phases — one enum replaces the scattered booleans (pending/started/ready/frozen).</summary>
	private enum RunPhase
	{
		Idle,         // menu — not following any run
		JoinPending,  // WorldJoin received, waiting for PreRunScript to start the run
		Starting,     // StartRun/StartTutorial called — transition/loading underway
		Generating,   // the generation boundary passed (the gen wrapper engaged)
		WaitingReady, // world generated — waiting for the host's WorldReady (gate freeze)
		Playing,      // WorldReady received — playing
	}

	private readonly ISessionControl _session = session;
	private readonly IWorldControl _world = world;
	private readonly IEntitySyncControl _entities = entities;
	private readonly CharacterDataSync _characterData = characterData;
	private readonly GuestMenuGuard _guestMenu = guestMenu;
	private readonly WorldParamsService _params = worldParams;
	private readonly ItemArbitration _arbitration = arbitration;
	private readonly ILogger<RunCoordinator> _log = log;

	private RunPhase _phase = RunPhase.Idle;
	private bool _joinIsTutorial; // the entry kind carried by the WorldJoin message
	private bool _hostStartGateAlertPending; // host: generation boundary → gate release — DoAlert popups in this span are deferred

	private bool _inWorld;
	private Body? _localBody; // Unity object — == (scene-reload check)

	/// <summary>Guest: when the host finished loading (its InWorld arrived via the SceneState relay) — the anchor for the guest-side 30 s countdown.</summary>
	private long _hostInWorldSinceMs;

	/// <summary>One-shot world fingerprint logged at world entry (peer log comparison).</summary>
	private bool _worldFingerprintLogged;

	/// <summary>The local body while in the world (Unity object — == null when scene-reload-destroyed).</summary>
	internal Body? LocalBody => _localBody;

	/// <summary>A world exists or is generating — the lobby-switch guard's window (menu-only lobby switches).</summary>
	internal bool IsInWorldOrGenerating => _inWorld || HarmonyTraverse.IsGenerating();

	/// <summary>Guest: the world is generated and the gate holds (read by StartGateCoordinator).</summary>
	internal bool GuestWaitingForReady => _phase == RunPhase.WaitingReady;

	/// <summary>Guest: the run is actually playing — the loading screen is the game's own to hide again (read by StartGateCoordinator).</summary>
	internal bool IsPlaying => _phase == RunPhase.Playing;

	/// <summary>
	/// The gate window — the finish-generation fade (WorldGeneration.cs:3620)
	/// must not black out the wait. Guest: generation finished (or finishing)
	/// but the gate still holds (the kept loading screen is underneath the
	/// fade). Host: the fade fires while the loading screen is still up (it is
	/// hidden at generation end, WorldGeneration.cs:3637 — the host's gate
	/// wait would otherwise read as "black, then the wait"). The GlobalDark
	/// patch skips the fade entirely inside this window.
	/// </summary>
	internal bool IsInGateWindow =>
		(_session.Role == SessionRole.Guest && _phase is RunPhase.Generating or RunPhase.WaitingReady)
		|| (_session.Role == SessionRole.Host && _session.SessionActive && HarmonyTraverse.IsLoadingVisible());

	/// <summary>
	/// The start-gate alert window — PlayerCamera.DoAlert popups are deferred
	/// here and replayed when the run is playing. Host: a local latch covers
	/// the generation boundary through gate release, including the moment the
	/// layer-title popup is built (WorldGeneration.cs:3640-3659) BEFORE the
	/// world-entry edge arms the gate. Guest: the follow phases Generating +
	/// WaitingReady cover the same span on the follower side.
	/// </summary>
	internal bool IsStartGateAlertWindow =>
		(_session.Role == SessionRole.Guest && _phase is RunPhase.Generating or RunPhase.WaitingReady)
		|| (_session.Role == SessionRole.Host && _hostStartGateAlertPending);

	/// <summary>Guest: when the host finished loading — the countdown anchor (read by StartGateCoordinator).</summary>
	internal long HostInWorldSinceMs => _hostInWorldSinceMs;

	internal void BindToSession()
	{
		_session.RemoteSceneChanged += OnRemoteSceneChanged;
		_session.SessionEnded += OnSessionEnded;
		_session.SessionActivated += OnSessionActivated;
		_world.WorldJoinReceived += OnWorldJoin;
		_world.WorldReadyReceived += OnRemoteWorldReady;
	}

	internal void Unbind()
	{
		_session.RemoteSceneChanged -= OnRemoteSceneChanged;
		_session.SessionEnded -= OnSessionEnded;
		_session.SessionActivated -= OnSessionActivated;
		_world.WorldJoinReceived -= OnWorldJoin;
		_world.WorldReadyReceived -= OnRemoteWorldReady;
	}

	/// <summary>Pump: scene state, guest menu lock, the WorldJoin follow retry, body state publishing.</summary>
	internal void Update()
	{
		UpdateSceneState();
		if (_phase == RunPhase.JoinPending)
		{
			TryStartWorldJoin();
		}

		if (_localBody != null) // Unity object — == (is null misses scene-reload-destroyed)
		{
			PublishBodyState(_localBody);
			_characterData.Update(_localBody);
			_characterData.UpdateRestore(_localBody);
		}
	}

	// ---- World join (host entry / guest follow) ----

	/// <summary>
	/// Host clicked start — capture the world params AT THE CLICK MOMENT and
	/// tell the members to start following immediately. The params are the
	/// generation baseline: both sides force Random.state back to them at the
	/// GenerateWorld boundary (ResetGenStreamToBaseline), so the guest holds
	/// them in hand right away instead of racing the host's boundary. The
	/// entry kind rides along for the guest's run choice (it cannot derive
	/// tutorial/run from the params — they carry it by construction).
	/// </summary>
	internal void OnWorldJoinRequested(bool isTutorial)
	{
		if (_session.Role == SessionRole.Host && _session.SessionActive)
		{
			_params.CaptureAtEntry(isTutorial); // the generation baseline — captured at the click moment, before any run randomness is consumed
			_characterData.ClearSavedCharacters(); // a NEW run — the previous run's saved characters are void (a stale restore would wipe the new run's starting supplies)
			_arbitration.ClearTransferred(); // a NEW run — the previous run's transfer entries are void (a stale entry would resurrect the old run's items on reconnect restore, #192)
			_world.SetHostRunPending(true); // mid-generation handshakes may follow immediately
			_world.SendWorldJoin(isTutorial);
		}
	}

	/// <summary>
	/// Called at the WorldGeneration.GenerateWorld boundary — the true start of
	/// generation. Host: consume the entry capture or capture at the boundary
	/// (anything the game consumed before this point is baked in). Guest:
	/// restore the host's params now, or hold (the generation wrapper waits for
	/// them before the coroutine consumes any Random) — the application point
	/// cannot move earlier because the host captures AT this boundary.
	/// </summary>
	internal void OnWorldGenerate()
	{
		if (_session.Role == SessionRole.Host || _session.Role == SessionRole.None)
		{
			if (_session.Role == SessionRole.Host)
			{
				_hostStartGateAlertPending = true; // the layer-title DoAlert at generation end must wait for the gate release
			}

			_params.OnGenerateBoundary();
		}
		else
		{
			_params.EnsureGuestApplied();
			if (_phase == RunPhase.Starting)
			{
				_phase = RunPhase.Generating;
			}
		}
	}

	/// <summary>
	/// Guest side: the host told us to enter the world (WorldJoin). The menu
	/// may still be loading when it arrives (a right-click "Join Game" launches
	/// a fresh process) — wait for PreRunScript, then start the run. The entry
	/// kind rides in the message (the world params do not exist yet at the
	/// moment the host clicks start, so the biome read would misjudge a
	/// tutorial for a run). Duplicate WorldJoin (a retried handshake answers
	/// each retry with a fresh copy) must not restart the run — a second
	/// StartRun starts a second WaitLoad/LoadScene coroutine over the first,
	/// corrupting the loading flow and diverging the generated world. The
	/// phase machine makes that structural: only Idle accepts a join.
	/// </summary>
	private void OnWorldJoin(bool isTutorial)
	{
		if (_phase != RunPhase.Idle)
		{
			return; // already following a run — duplicate instruction ignored
		}

		_joinIsTutorial = isTutorial;
		_phase = RunPhase.JoinPending;
		TryStartWorldJoin();
	}

	private void TryStartWorldJoin()
	{
		if (_inWorld)
		{
			return;
		}

		if (HarmonyTraverse.IsGenerating())
		{
			// The host re-broadcast WorldJoin (its layer switch re-runs
			// GenerateWorld) while our own generation is running — we are
			// already loading. Our layer switch is our own ContinueRun, never
			// the host's instruction.
			_phase = RunPhase.Idle;
			return;
		}

		if (PreRunScript.instance == null) // Unity object — == (menu still loading)
		{
			return; // stay in JoinPending — the pump retries
		}

		_phase = RunPhase.Starting;
		_guestMenu.AuthorizeNextStart(); // the gate refuses unauthorised (manual) guest starts
		_log.LogInformation("World join received — starting {Run} to follow.", _joinIsTutorial ? "the tutorial" : "a run");
		if (_joinIsTutorial)
		{
			// StartTutorial sets the tutorial flag and nulls runSettings itself
			// (PreRunScript.cs:307-314).
			PreRunScript.instance.StartTutorial();
		}
		else
		{
			PreRunScript.instance.StartRun();
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
		if (!inWorld)
		{
			// Leaving the world (death, menu) — the run-follow resets: a
			// future WorldJoin may follow a new run, and the gate can never
			// release us here.
			_phase = RunPhase.Idle;
			_hostStartGateAlertPending = false; // a stale deferred layer-title must not replay into the menu
		}

		if (inWorld && !_worldFingerprintLogged)
		{
			_worldFingerprintLogged = true;
			LogWorldFingerprint();
		}

		var prevBody = _localBody; // Unity object — ==
		_localBody = inWorld ? PlayerCamera.main!.body : null;
		if (!inWorld && prevBody != null)
		{
			_characterData.NotifyBodyLeft(prevBody);
		}

		if (inWorld && _localBody != null) // Unity object — ==
		{
			// Apply the restore position BEFORE reporting the spawn position —
			// the host spawns the clone at ReportedSpawnPos, so reporting the
			// pre-restore landing spot made the host's clone appear there and
			// then jump (observed: the reconnecting guest's clone teleported on
			// the host's screen). The position gate (#7) keeps this idempotent.
			_characterData.ApplyPendingPositionOnly(_localBody);
		}

		if (inWorld && _session.Role == SessionRole.Host)
		{
			// The run is now actually in the world — the world-entry condition
			// (LocalSceneState == InWorld) takes over for later handshakes.
			_world.SetHostRunPending(false);
			// Members that joined mid-generation never received the entry
			// WorldJoin (it fires at the click moment, before they handshook) —
			// invite them now that the world is up (only members not yet in the
			// world are targeted; a re-send to an already-starting member is
			// ignored by its run-start gate).
			_world.SendWorldJoin(_world.WorldParams?.IsTutorial ?? false);
			if (_world.StartStartGate())
			{
				_log.LogInformation("Host entered the world — waiting for members before everyone starts.");
			}
			else
			{
				// Nobody to wait for (no end-to-end handshaken member — the
				// handshake may still be failing): the run starts at once. The
				// gate-release path (MarkPlayingForHost, StartGateCoordinator)
				// only runs when a gate was actually ARMED, so without this the
				// host's phase would stay Generating forever and the
				// loading-screen keeper (ShouldKeep = SessionActive && !IsPlaying)
				// would pin the loading screen over a running game.
				MarkPlayingForHost();
				_log.LogInformation("Host entered the world — no one to wait for, starting immediately.");
			}
		}

		if (inWorld && _session.Role == SessionRole.Guest && _phase is RunPhase.Starting or RunPhase.Generating)
		{
			// The world is generated and we are not playing yet — wait for the
			// host's WorldReady (the gate freeze engages here).
			_phase = RunPhase.WaitingReady;
		}

		var sceneName = inWorld ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name : "PreGen";
		var pos = inWorld && _localBody != null // Unity object — ==
			? new NetVector2(_localBody.transform.position.x, _localBody.transform.position.y)
			: (NetVector2?)null;
		_session.ReportSceneState(inWorld ? SceneStateType.InWorld : SceneStateType.InMenu, sceneName, pos);
	}

	/// <summary>
	/// One-shot world fingerprint: FNV-1a over the block table, per-128-row
	/// blocks + a total — the peer logs' fingerprints show whether the two
	/// generated worlds match and, when they diverge, roughly where.
	/// </summary>
	private void LogWorldFingerprint()
	{
		var blocks = HarmonyTraverse.ReadWorldBlocks(WorldGeneration.world);
		if (blocks is null) // Unity object — ==
		{
			return;
		}

		const ulong fnvBasis = 14695981039346656037UL;
		const ulong fnvPrime = 1099511628211UL;
		var width = blocks.GetLength(0);
		var height = blocks.GetLength(1);
		var blockHashes = new ulong[8];
		for (var i = 0; i < blockHashes.Length; i++)
		{
			blockHashes[i] = fnvBasis;
		}

		var rowsPerBlock = Math.Max(1, height / 8);
		var total = fnvBasis;
		for (var y = 0; y < height; y++)
		{
			var b = Math.Min(7, y / rowsPerBlock);
			for (var x = 0; x < width; x++)
			{
				var v = blocks[x, y];
				total ^= v;
				total *= fnvPrime;
				blockHashes[b] ^= v;
				blockHashes[b] *= fnvPrime;
			}
		}

		_log.LogInformation(
			"[WorldFingerprint] {W}x{H}: {B0:X16} {B1:X16} {B2:X16} {B3:X16} {B4:X16} {B5:X16} {B6:X16} {B7:X16} total {Total:X16}",
			width, height, blockHashes[0], blockHashes[1], blockHashes[2], blockHashes[3],
			blockHashes[4], blockHashes[5], blockHashes[6], blockHashes[7], total);
	}

	/// <summary>Guest side: the host released the start gate — start playing.</summary>
	private void OnRemoteWorldReady()
	{
		if (_phase == RunPhase.WaitingReady)
		{
			_phase = RunPhase.Playing;
		}

		_log.LogInformation("World ready — start playing.");
	}

	/// <summary>Host side: the start gate released — the host never receives a
	/// WorldReady for itself, so its phase would otherwise never leave
	/// Generating (the loading-screen keeper's !IsPlaying gate would keep the
	/// loading screen up over a running game). Also closes the start-gate alert
	/// window: StartGateCoordinator replays the deferred popups on the next pump.</summary>
	internal void MarkPlayingForHost()
	{
		_phase = RunPhase.Playing;
		_hostStartGateAlertPending = false;
	}

	// ---- Session wiring ----

	/// <summary>
	/// Re-report the scene state (with position) once the handshake completes —
	/// if we entered the world before connecting, the earlier report was not
	/// sent (no session yet) and the host would spawn our clone at (0,0).
	/// </summary>
	private void OnSessionActivated()
	{
		// The session ended when the last member left the lobby (the host kept
		// playing in its world); a reconnect re-activates it while we are still
		// in the world — resume playing. Without this the phase stays Idle:
		// the loading-screen keeper (!IsPlaying gate) pins the loading screen
		// over the running game and nothing ever releases it (the late joiner
		// is passed in directly, which never touches the host's phase).
		if (_session.Role == SessionRole.Host && _phase == RunPhase.Idle && _inWorld)
		{
			_phase = RunPhase.Playing;
			_log.LogInformation("Reconnect — the host is still in the world, resuming playing.");
		}

		if (!_inWorld || _localBody == null) // Unity object — ==
		{
			return;
		}

		var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
		var pos = new NetVector2(_localBody.transform.position.x, _localBody.transform.position.y);
		_session.ReportSceneState(SceneStateType.InWorld, sceneName, pos);
	}

	/// <summary>
	/// The host leaving the world ends the world itself (host authority): a
	/// guest must not keep playing inside a world whose owner is gone — pull it
	/// back to the main menu. Only when we are actually in the world; after the
	/// load the normal UpdateSceneState flow re-reports InMenu to the host.
	/// (The render clone teardown for ANY member lives in RemotePlayerRenderer.)
	/// </summary>
	private void OnRemoteSceneChanged(ulong steamId, bool inWorld)
	{
		if (inWorld && steamId == _session.HostSteamId)
		{
			_hostInWorldSinceMs = Environment.TickCount; // guest countdown anchor: the host finished loading
		}

		if (!inWorld && steamId == _session.HostSteamId && _session.Role == SessionRole.Guest)
		{
			_phase = RunPhase.Idle;
			if (_inWorld && PlayerCamera.main != null) // Unity object — ==
			{
				_log.LogInformation("Host left the world — returning to main menu.");
				PlayerCamera.main.ToMainMenu();
			}
		}
	}

	/// <summary>
	/// The session is gone — the run-follow ends and every run-scoped flag dies
	/// with it. A former HOST that was still in its world returns to the main
	/// menu here (a guest is pulled by the host's RemoteSceneChanged false edge
	/// above); a follow instruction for the NEXT lobby may already be pending,
	/// and the pump retries it once the menu transition completes.
	/// </summary>
	private void OnSessionEnded()
	{
		_phase = RunPhase.Idle;
		_joinIsTutorial = false;
		_hostStartGateAlertPending = false;
		_hostInWorldSinceMs = 0;
		_worldFingerprintLogged = false;
		_params.ResetForSessionEnd();
		if (_inWorld && PlayerCamera.main != null) // Unity object — ==
		{
			_log.LogInformation("Session ended while in the world — returning to main menu.");
			PlayerCamera.main.ToMainMenu();
		}
	}

	// ---- State shuttling ----

	private void PublishBodyState(Body body)
	{
		var pos = body.transform.position;
		var look = body.targetLookPos;
		var vel = body.rb.velocity;
		// LookTarget/CorpseScript drive Body.overrideLookPos/Time on the local
		// player (LookTarget.cs:12-13, CorpseScript.cs:87-88); carry the
		// override target and the scared-face timer with the 20 Hz state so a
		// remote clone visibly looks at the same enemy/corpse and shows the
		// same facial expression.
		var lookOverridePos = body.overrideLookTime > 0f
			? new NetVector2(body.overrideLookPos.x, body.overrideLookPos.y)
			: (NetVector2?)null;
		// Pose flags mirror the game's own pose rules:
		// - sitting: idle sit condition (Body.cs:3162), minus movingAllowed
		//   (private; sleeping is covered by the sleeping flag).
		// - sleeping: Body.cs:3961.
		// - climbing: currentClimbable (Body.cs:470).
		var sitting = body.idleTime > 12f && !body.exercising;
		// Workout/exercise: Body.DoWorkout is a coroutine that does not expose
		// the active workout type as a public field, so BodyWorkoutPatch stores
		// the requested WorkoutType on a tiny local-body tracker. The
		// Body.exercising flag is the authoritative "currently working out"
		// gate — a failed DoWorkout guard or a stopped coroutine never sends a
		// stale workout pose.
		var workoutType = body.exercising
			&& body.TryGetComponent<LocalWorkoutTracker>(out var workoutTracker)
				? workoutTracker.WorkoutType
				: (byte)0;
		_entities.PublishLocalState(
			new NetVector2(pos.x, pos.y),
			new NetVector2(look.x, look.y),
			new NetVector2(vel.x, vel.y),
			body.isRight, body.standing, body.alive, body.conscious, body.crouching,
			lookOverridePos, body.overrideLookTime, body.eyeScareTime,
			body.eyePanicTime, body.eyeCloseTime,
			sitting, body.sleeping, body.currentClimbable != null, // Unity object — ==
			workoutType);
	}
}
