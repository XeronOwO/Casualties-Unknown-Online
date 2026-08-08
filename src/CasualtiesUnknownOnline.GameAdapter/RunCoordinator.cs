using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using Random = UnityEngine.Random;

using System;

using CasualtiesUnknownOnline.GameAdapter.WorldGen;

namespace CasualtiesUnknownOnline.GameAdapter;

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
	SessionService session,
	WorldService world,
	EntitySyncService entities,
	CharacterDataSync characterData,
	GuestMenuGuard guestMenu,
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

	private readonly SessionService _session = session;
	private readonly WorldService _world = world;
	private readonly EntitySyncService _entities = entities;
	private readonly CharacterDataSync _characterData = characterData;
	private readonly GuestMenuGuard _guestMenu = guestMenu;
	private readonly ILogger<RunCoordinator> _log = log;

	private RunPhase _phase = RunPhase.Idle;
	private bool _joinIsTutorial; // the entry kind carried by the WorldJoin message

	private bool _inWorld;
	private Body? _localBody; // Unity object — == (scene-reload check)

	/// <summary>Host: params captured at the run-start entry — the first GenerateWorld must not re-capture.</summary>
	private bool _entryParamsCaptured;

	/// <summary>Guest: the params instance whose Random.state is currently restored (a new instance = a new world/layer = re-apply).</summary>
	private WorldStartParams? _appliedWorldParams;
	private bool _guestParamsWaitLogged; // guest: the "generation holding for params" log fired for this wait

	/// <summary>Guest: when the host finished loading (its InWorld arrived via the SceneState relay) — the anchor for the guest-side 30 s countdown.</summary>
	private long _hostInWorldSinceMs;

	/// <summary>The local body while in the world (Unity object — == null when scene-reload-destroyed).</summary>
	internal Body? LocalBody => _localBody;

	/// <summary>Guest: the world is generated and the gate holds (read by StartGateCoordinator).</summary>
	internal bool GuestWaitingForReady => _phase == RunPhase.WaitingReady;

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
			CaptureWorldParamsAtEntry(isTutorial);
			_world.SetHostRunPending(true); // mid-generation handshakes may follow immediately
			_world.SendWorldJoin(isTutorial);
		}
	}

	/// <summary>
	/// Host only: capture + publish the world params at the run-start entry
	/// (the click moment), BEFORE any run randomness is consumed — the
	/// generation stream is force-reset to this baseline at the GenerateWorld
	/// boundary, so capturing now is equivalent to capturing there, and the
	/// guests get the params with zero waiting. Run settings come from the
	/// menu (PreRunScript — WorldGeneration.runSettings is only assigned inside
	/// StartRun); the tutorial nulls them itself (PreRunScript.cs:312). The
	/// world-defining fields are all defaults at the entry: biomeOverride
	/// follows the entry kind (tutorial or not — its other source is the
	/// WorldGeneration.Awake tutorial flag, identical on both sides), depth and
	/// traveled start at 0 (debugStartDepth is a debug-console value).
	/// </summary>
	private void CaptureWorldParamsAtEntry(bool isTutorial)
	{
		_world.ResetDamagedBlocks(); // the new run's damage table starts empty again

		var randomState = RandomStateSerializer.Serialize(Random.state);
		_world.PublishWorldParams(new WorldStartParams
		{
			RandomState = randomState,
			RunSettings = isTutorial ? null : HarmonyTraverse.ReadPreRunRunSettings(),
			BiomeOverride = isTutorial ? (byte)WorldGeneration.OverrideSceneType.Tutorial : (byte)WorldGeneration.OverrideSceneType.None,
			BiomeDepth = 0,
			TotalTraveled = 0,
		});
		_entryParamsCaptured = true;
		_log.LogInformation("Captured world params at run-start entry ({StateBytes} bytes, tutorial: {Tutorial}).",
			randomState.Length, isTutorial);
	}

	/// <summary>Host side: capture + publish the world params at the GenerateWorld boundary (layer switches, solo, load-run — the entry capture does not apply).</summary>
	internal void CaptureWorldParams()
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

	/// <summary>Guest side: restore the host's RNG state + run settings + world-defining fields so local world generation produces the same world.</summary>
	internal void ApplyWorldParams(WorldStartParams parameters)
	{
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

	/// <summary>
	/// Guest side, called before the generation coroutine may consume any
	/// Random: false while the host's world params have not arrived (the
	/// wrapper holds the coroutine — nothing random consumed yet); on arrival
	/// restores them and returns true. Idempotent per params instance — a layer
	/// switch delivers a new instance and re-applies. Host/solo: nothing to
	/// wait for.
	/// </summary>
	internal bool EnsureGuestWorldParams()
	{
		if (_session.Role != SessionRole.Guest)
		{
			return true;
		}

		var parameters = _world.WorldParams;
		if (parameters is null)
		{
			// The wrapper polls every frame — log the hold once per wait, so a
			// held generation is observable without spamming the log.
			if (!_guestParamsWaitLogged)
			{
				_guestParamsWaitLogged = true;
				_log.LogInformation("World generation holding — host world params not arrived yet (fast guest transition).");
			}

			return false;
		}

		_guestParamsWaitLogged = false; // re-arm for the next world/layer
		if (!ReferenceEquals(_appliedWorldParams, parameters))
		{
			ApplyWorldParams(parameters);
			_appliedWorldParams = parameters;
		}

		return true;
	}

	/// <summary>
	/// Both sides: force Random.state back to the captured baseline right before
	/// the generation coroutine starts. The host captured it at its run-start
	/// entry — everything consumed between that moment and here (transition,
	/// scene loading, WorldGeneration.Start) is overwritten, keeping the two
	/// generation streams identical. Guest: the params were just applied by
	/// <see cref="EnsureGuestWorldParams"/> — same value, idempotent.
	/// </summary>
	internal void ResetGenStreamToBaseline()
	{
		var parameters = _world.WorldParams;
		if (parameters is null)
		{
			return;
		}

		Random.state = RandomStateSerializer.Deserialize(parameters.RandomState);
		_log.LogInformation("Generation stream reset to captured baseline ({StateBytes} bytes).", parameters.RandomState.Length);
	}

	/// <summary>
	/// Called at the WorldGeneration.GenerateWorld boundary — the true start of
	/// generation. Host captures its RNG state (and any randomness consumed
	/// before this point is baked in); guest restores the host's state so both
	/// generate the identical world.
	/// </summary>
	internal void OnWorldGenerate()
	{
		if (_session.Role == SessionRole.Host || _session.Role == SessionRole.None)
		{
			if (_entryParamsCaptured)
			{
				// First generation of a run that captured its params at the
				// click moment — re-capturing here would move the baseline
				// and re-send, racing the guests' already-started runs.
				_entryParamsCaptured = false;
			}
			else
			{
				CaptureWorldParams(); // layer switch (or solo/load-run): capture at the boundary
			}
		}
		else
		{
			// Guest: apply the params now, or hold (the generation wrapper
			// waits for them before the coroutine consumes any Random). The
			// host captures its params AT this boundary (anything the game
			// consumed before it is baked in) — the guest must restore the
			// exact same moment, so the application point cannot move earlier.
			EnsureGuestWorldParams();
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
		}

		var prevBody = _localBody; // Unity object — ==
		_localBody = inWorld ? PlayerCamera.main!.body : null;
		if (!inWorld && prevBody != null)
		{
			_characterData.NotifyBodyLeft(prevBody);
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

	/// <summary>Guest side: the host released the start gate — start playing.</summary>
	private void OnRemoteWorldReady()
	{
		if (_phase == RunPhase.WaitingReady)
		{
			_phase = RunPhase.Playing;
		}

		_log.LogInformation("World ready — start playing.");
	}

	// ---- Session wiring ----

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

	/// <summary>The session is gone — the run-follow ends (the gate presentation
	/// restores itself on its next pump; the render clone teardown lives in
	/// RemotePlayerRenderer).</summary>
	private void OnSessionEnded() => _phase = RunPhase.Idle;

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
}
