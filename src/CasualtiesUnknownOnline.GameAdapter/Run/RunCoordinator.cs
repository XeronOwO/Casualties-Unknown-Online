using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.World;
using Microsoft.Extensions.Logging;
using HarmonyLib;
using UnityEngine.SceneManagement;

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
	IPlayerInteractionControl playerInteraction,
	IWorldSaveControl worldSaves,
	IItemControl items,
	WorldRestoreAudit? restoreAudit,
	StartingSupplyCoordinator startingSupplies,
	RunMenuReturnCoordinator menuReturn,
	LayerAdvanceCoordinator layerAdvance,
	DescentBodyPreservation descentBody,
	ILogger<RunCoordinator> log)
{
	/// <summary>Guest run-follow phases — one enum replaces the scattered booleans (pending/started/ready/frozen).</summary>
	private enum RunPhase
	{
		Idle,         // menu — not following any run
		JoinPending,  // WorldJoin received, waiting for PreRunScript to start the run
		Starting,     // StartRun/StartTutorial called — transition/loading underway
		Generating,   // the generation boundary passed (the gen wrapper engaged)
		Descending,   // the host announced its own layer advance — this member generates its new layer in parallel
		WaitingReady, // world generated — waiting for the host's WorldReady (gate freeze)
		Playing,      // WorldReady received — playing
	}

	/// <summary>
	/// Guest: the host descent follow's own state — the piece the run phase does not carry (the phase
	/// says what the CLIENT shows, this says how far the descent's generation got).
	/// </summary>
	private enum DescentPhase
	{
		None,          // not following a host descent
		Accepted,      // the edge was taken; the local regeneration has not launched yet (the baseline hold)
		Launched,      // the generation actually started (the gen wrapper left its hold)
		ReleasedEarly, // the host released the gate while this generation was still running
		WaitingReady,  // the generation settled — waiting for the host's release
	}

	private readonly ISessionControl _session = session;
	private readonly IWorldControl _world = world;
	private readonly IEntitySyncControl _entities = entities;
	private readonly CharacterDataSync _characterData = characterData;
	private readonly GuestMenuGuard _guestMenu = guestMenu;
	private readonly WorldParamsService _params = worldParams;
	private readonly ItemArbitration _arbitration = arbitration;
	private readonly IPlayerInteractionControl _playerInteraction = playerInteraction;
	private readonly StartingSupplyCoordinator _startingSupplies = startingSupplies;
	private readonly RunSaveCoordinator _save = new(session, world, worldSaves, worldParams, characterData, items, restoreAudit, startingSupplies, log);
	private readonly LayerAdvanceCoordinator _layerAdvance = layerAdvance;
	private readonly DescentBodyPreservation _descentBody = descentBody;
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

	/// <summary>
	/// Guest: how far the host descent follow got. Non-<see cref="DescentPhase.None"/> from the edge
	/// until the gate releases; it is what keeps the world's not-live edge a descent instead of a menu
	/// exit and what makes the host's InMenu report expected rather than a pull back to the menu.
	/// </summary>
	private DescentPhase _descent;

	/// <summary>Guest: when the descent edge was accepted — the anchor for the follow's bounded waits (baseline hold, settle).</summary>
	private int _descentAcceptedAtMs;

	/// <summary>
	/// Host: this host's own regeneration is under way. It opens the presentation window before the
	/// loading screen is visible (the descent's own fade) and closes with the gate release.
	/// </summary>
	private bool _hostDescentPending;

	/// <summary>
	/// Pending return-to-menu after a session teardown. Session-ended events
	/// run inside Steam/UI callbacks, so the actual scene load is deferred to the
	/// frame-end pump (<see cref="SaveCutSeam"/>) — loaded during OnGUI was the
	/// host-close exit path. It is the same seam a cut is taken at, because the
	/// host's deliberate return IS a cut.
	/// </summary>
	private readonly RunMenuReturnCoordinator _menuReturn = menuReturn;

	/// <summary>The local body while in the world (Unity object — == null when scene-reload-destroyed).</summary>
	internal Body? LocalBody => _localBody;

	/// <summary>A world exists or is generating — the lobby-switch guard's window (menu-only lobby switches).</summary>
	internal bool IsInWorldOrGenerating => _inWorld || HarmonyTraverse.IsGenerating();

	/// <summary>The world is up and not generating — the precondition the frame-end cut seam checks before it asks for a cut.</summary>
	internal bool IsInWorld => _inWorld;

	/// <summary>
	/// Guest: a run-entry instruction has been accepted and this side's world is not up yet
	/// (join pending / scene load / generation). A one-shot generation snapshot can arrive in this
	/// window before the world object exists, so it must be HELD, not dropped (adversarial review
	/// major-1); <see cref="GeneratedItemApplication"/> consumes this fact with its hold condition.
	/// </summary>
	internal bool IsEnteringWorld => _phase is RunPhase.JoinPending or RunPhase.Starting or RunPhase.Generating or RunPhase.Descending;

	/// <summary>Guest: this member is following the host's own layer advance — its not-live world is the descent, not a menu exit (read by the re-entry window and the menu-return path).</summary>
	internal bool IsDescending => _descent != DescentPhase.None;

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
		(_session.Role == SessionRole.Guest && _phase is RunPhase.Descending or RunPhase.Generating or RunPhase.WaitingReady)
		|| (_session.Role == SessionRole.Host && _session.SessionActive && (_hostDescentPending || HarmonyTraverse.IsLoadingVisible()));

	/// <summary>
	/// The start-gate alert window — PlayerCamera.DoAlert popups are deferred
	/// here and replayed when the run is playing. Host: a local latch covers
	/// the generation boundary through gate release, including the moment the
	/// layer-title popup is built (WorldGeneration.cs:3640-3659) BEFORE the
	/// world-entry edge arms the gate. Guest: the follow phases Generating +
	/// WaitingReady cover the same span on the follower side.
	/// </summary>
	internal bool IsStartGateAlertWindow =>
		(_session.Role == SessionRole.Guest && _phase is RunPhase.Descending or RunPhase.Generating or RunPhase.WaitingReady)
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
		_world.LayerDescentReceived += OnLayerDescent;
	}

	internal void Unbind()
	{
		_session.RemoteSceneChanged -= OnRemoteSceneChanged;
		_session.SessionEnded -= OnSessionEnded;
		_session.SessionActivated -= OnSessionActivated;
		_world.WorldJoinReceived -= OnWorldJoin;
		_world.WorldReadyReceived -= OnRemoteWorldReady;
		_world.LayerDescentReceived -= OnLayerDescent;
	}

	/// <summary>Pump: scene state, guest menu lock, the WorldJoin follow retry, body state publishing. The menu return and the cuts are taken by <see cref="SaveCutSeam"/> at the frame-end seam.</summary>
	internal void Update()
	{
		UpdateDescentAbort();
		UpdateDescent();
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

	// ---- Host layer descent ----

	/// <summary>
	/// The follow's ways out besides the settle: the member left the world (a real leave or the death
	/// menu destroys the world object — the follow must then hand the client back to the ordinary entry
	/// path, because its phase refuses every WorldJoin), or one of its own waits outlived its bound (the
	/// host's baseline never arrived, the generation never settled). Without this a member that leaves
	/// mid-descent stays out of the session for good (the independent review's blocker).
	/// </summary>
	private void UpdateDescentAbort()
	{
		if (_descent == DescentPhase.None)
		{
			return;
		}

		var elapsedMs = Environment.TickCount - _descentAcceptedAtMs;
		var holdTimedOut = _descent == DescentPhase.Accepted && elapsedMs > LayerDescentPolicy.HoldTimeoutMs;
		var settleTimedOut = _descent is DescentPhase.Launched or DescentPhase.ReleasedEarly && elapsedMs > LayerDescentPolicy.SettleTimeoutMs;
		var hasWorld = HarmonyTraverse.HasWorld;
		if (!LayerDescentPolicy.ShouldAbort(hasWorld, holdTimedOut, settleTimedOut))
		{
			return;
		}

		AbortDescent(
			!hasWorld ? "the world was left (a leave or a scene change) mid-descent"
			: holdTimedOut ? "the host's baseline for the new layer never arrived"
			: "the new layer's generation never settled",
			leaveToMenu: hasWorld);
	}

	/// <summary>Ends the follow and hands the member back to the ordinary entry path; a half-built world with no way forward is left to the menu, the same exit the start gate's valve uses.</summary>
	private void AbortDescent(string reason, bool leaveToMenu)
	{
		_descent = DescentPhase.None;
		_descentBody.Clear();
		_phase = RunPhase.Idle;
		_log.LogWarning("[LayerDescent] the follow is abandoned — {Reason}; the ordinary entry path takes over.", reason);
		if (leaveToMenu && PlayerCamera.main != null) // Unity object — ==
		{
			PlayerCamera.main.ToMainMenu();
		}
	}

	/// <summary>Accepts the host's descent and starts this member's own new-layer generation (the runtime already dropped the baseline for the layer being left).</summary>
	private void AcceptDescent(bool twice)
	{
		_descent = DescentPhase.Accepted;
		_descentAcceptedAtMs = Environment.TickCount;
		var descentBody = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		_descentBody.Note(descentBody != null ? descentBody.GetInstanceID() : 0);
		_phase = RunPhase.Descending;
		_log.LogInformation("[LayerDescent] host descent accepted (twice: {Twice}) — this member runs its own new-layer generation and waits for the gate instead of returning to the menu.", twice);
		_layerAdvance.BeginLocalDescent(twice);
	}

	/// <summary>
	/// The descent's state machine. Guest: the settle edge — the generation actually started (the gen
	/// wrapper left its baseline hold) and the world is live again — moves the member to
	/// <see cref="RunPhase.WaitingReady"/>, or straight to Playing when the host released the gate while
	/// this member was still generating. Runs BEFORE the scene-state edge so the settle frame's
	/// transition is evaluated with the phase it belongs to (and so no report is sent for the transient
	/// "world object exists but the generation has not launched" frame of the wrapper's hold).
	/// </summary>
	private void UpdateDescent()
	{
		if (_descent is DescentPhase.None or DescentPhase.WaitingReady)
		{
			return;
		}

		if (!LayerDescentPolicy.IsSettled(_descent != DescentPhase.Accepted, HarmonyTraverse.IsGenerating(), HarmonyTraverse.IsRegenerating(), HarmonyTraverse.HasLiveWorld))
		{
			return;
		}

		if (_descent == DescentPhase.ReleasedEarly)
		{
			_descent = DescentPhase.None;
			_phase = RunPhase.Playing;
			_log.LogInformation("[LayerDescent] the new layer is generated and the host's release was already in hand — this member starts playing.");
			return;
		}

		_descent = DescentPhase.WaitingReady;
		_phase = RunPhase.WaitingReady;
		_log.LogInformation("[LayerDescent] this member's new layer is generated — waiting for the host's release on the loading screen.");
	}

	/// <summary>
	/// Guest: the host's descent edge. Accepted from a member that is PLAYING (or waiting on this
	/// descent's gate when the host advances again) in a live world; a member in the menu, mid-entry, or
	/// with its own generation in flight follows the ordinary entry path instead. A pending menu return
	/// is cancelled only when it came from the host's own InMenu report — the player's own leave is never
	/// consumed by a descent.
	/// </summary>
	private void OnLayerDescent(bool twice)
	{
		if (_descent == DescentPhase.WaitingReady)
		{
			// The host advanced again before releasing this group: the first layer is already generated
			// and only the release was outstanding, so this edge starts the next one.
			if (!TryClearHostPullMenuReturn())
			{
				return;
			}

			AcceptDescent(twice);
			return;
		}

		var generationInFlight = _descent is DescentPhase.Accepted or DescentPhase.Launched or DescentPhase.ReleasedEarly;
		var decision = LayerDescentPolicy.DecideFollow(
			_session.Role,
			_session.SessionActive,
			_inWorld,
			_phase is RunPhase.Playing or RunPhase.WaitingReady,
			generationInFlight,
			HarmonyTraverse.HasWorld);
		switch (decision)
		{
			case LayerDescentDecision.Follow:
				break;

			case LayerDescentDecision.NotThisSides:
				_log.LogDebug("[LayerDescent] a descent edge reached a side that is not a live guest — dropped.");
				return;

			case LayerDescentDecision.AlreadyDescending:
				_log.LogWarning("[LayerDescent] a second descent edge arrived while this member's generation is still in flight — ignored; it generates the layer the host announced first.");
				return;

			case LayerDescentDecision.NotPlaying:
				_log.LogInformation("[LayerDescent] the host descended but this member is not playing in its world ({Phase}, in world: {InWorld}) — it keeps the ordinary entry path.", _phase, _inWorld);
				return;

			default:
				_log.LogWarning("[LayerDescent] the host descended but this member has no world object to regenerate — it keeps the ordinary entry path.");
				return;
		}

		if (!TryClearHostPullMenuReturn())
		{
			return;
		}

		AcceptDescent(twice);
	}

	/// <summary>
	/// The descent may cancel a menu return recorded from the host's own InMenu report (the edge and the
	/// report travel one reliable lane, edge first, but a frame-end seam must never load the menu under a
	/// descent). Any other origin is not the descent's to consume — in particular the player's own leave,
	/// which must still strand nobody. Returns false when the member must not follow the edge.
	/// </summary>
	private bool TryClearHostPullMenuReturn()
	{
		if (!_menuReturn.IsPending)
		{
			return true;
		}

		if (_menuReturn.Origin != RunMenuReturnOrigin.HostPull)
		{
			_log.LogInformation("[LayerDescent] the player's own leave is pending — the descent does not pull this member through it.");
			return false;
		}

		_menuReturn.Clear();
		return true;
	}

	/// <summary>Guest: the descent's generation actually started — the runtime's generation-stream reset is the launch edge, after the wrapper's baseline hold has ended.</summary>
	internal void OnLocalGenerationStarted()
	{
		if (_descent == DescentPhase.Accepted)
		{
			_descent = DescentPhase.Launched;
		}
	}

	/// <summary>Host: a local regeneration this side owns is starting — open the presentation window before the loading screen is visible, so the descent's own fade is skipped instead of reading as a black frame.</summary>
	internal void OnHostDescentStarted()
	{
		if (_session.Role == SessionRole.Host && _session.SessionActive)
		{
			_hostDescentPending = true;
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
		// Host AND solo: this run gets its own world folder before any content
		// exists (the save arm owns that call — see RunSaveCoordinator) — except for
		// the tutorial, which gets no archive at all.
		_save.BeginRun(isTutorial);

		if (_session.Role == SessionRole.Host && _session.SessionActive)
		{
			_params.CaptureAtEntry(isTutorial); // the generation baseline — captured at the click moment, before any run randomness is consumed
			_characterData.ClearSavedCharacters(); // a NEW run — the previous run's saved characters are void (a stale restore would wipe the new run's starting supplies)
			_arbitration.ClearTransferred(); // a NEW run — the previous run's transfer entries are void (a stale entry would resurrect the old run's items on reconnect restore, #192)
			_world.SetHostRunPending(true); // mid-generation handshakes may follow immediately
			_world.SendWorldJoin(isTutorial);
		}
	}

	/// <summary>The native Continue entry: the save arm restores the selected CUO world (and blocks the original when it cannot).</summary>
	internal bool OnHostContinueRequested() => _save.OnContinueRequested();

	/// <summary>The local body's character snapshot right now — the cut payload's live half (the frame-end seam calls this at the instant of the cut).</summary>
	internal CharacterDataMsg? CaptureLocalCharacter() => _save.CaptureLocal(_localBody);


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
			// Already following a run — duplicate instruction ignored.
			if (_descent != DescentPhase.None)
			{
				// This is the host's own world-entry invite, sent to members it still reads as out of
				// the world: this member is NOT following it (it is generating the same layer from the
				// descent). The invite's baseline promise dropped the params its generation is using —
				// put them back. The kernel checkpoint that follows is the host's current session state;
				// its world projections are deferred by their own generation guards until this member's
				// generation settles, so it cannot replay into the rebuilding world.
				_world.CancelWorldJoinBaselineDrop();
				_log.LogInformation("[LayerDescent] ignored the host's world-entry invite while following the descent — the baseline its promise dropped was restored.");
			}

			return;
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
			// This client generates for a reason of its own (a run start, or a producer
			// that owns its descent): the instruction belongs to a layer this side is
			// already leaving. A member's end-of-layer choice is no longer such a
			// reason — it is delegated, and the session re-invites this client.
			_phase = RunPhase.Idle;
			return;
		}

		if (PreRunScript.instance == null) // Unity object — == (menu still loading)
		{
			return; // stay in JoinPending — the pump retries
		}

		_phase = RunPhase.Starting;
		_guestMenu.AuthorizeNextStart(); // the gate refuses unauthorised (manual) guest starts

		// This client is starting a run it did NOT restore (the host's, not a CUO continue of
		// its own): a local character restore queued by a refused or abandoned continue on this
		// machine must never land on a body that belongs to someone else's run. Same rule as
		// RunSaveCoordinator.BeginRun applies to a run started here.
		_characterData.CancelOwnRunRestore();
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
		// The expression is shared with the seams that act on the same edge (the
		// menu-exit scene-load interception) so "in world" cannot drift.
		// A descent that is still generating is NOT in a world even when the game's object exists: the
		// generation wrapper can hold GenerateWorld with generatingWorld already down (the baseline hold),
		// and reporting that frame as InWorld would let the host release the gate before this member's
		// new layer exists.
		var descentUnsettled = _descent is DescentPhase.Accepted or DescentPhase.Launched or DescentPhase.ReleasedEarly;
		var inWorld = HarmonyTraverse.HasLiveWorld && !descentUnsettled;
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
			// release us here. A descent's not-live edge is the transition it
			// is here to make, so its phase survives it.
			if (_descent == DescentPhase.None)
			{
				_phase = RunPhase.Idle;
				_descentBody.Clear(); // a real exit: the next body is not the descent's
			}

			_hostStartGateAlertPending = false; // a stale deferred layer-title must not replay into the menu
		}

		if (inWorld && !_worldFingerprintLogged)
		{
			_worldFingerprintLogged = true;
			WorldFingerprintLog.Log(_log);
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

		if (inWorld)
		{
			// The world-entry edge: write the run clock a peer sent, then publish this side's settled clocks (RunClockFactsSync, owned by WorldParams).
			_params.PublishRunClockFactsAtEntry();
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

		// The report names the ACTUAL active scene, role and state included: "not in world" is not
		// synonymous with the menu — during a layer descent this member is still in the world's scene
		// with no live world, and the old hardcoded "PreGen" made that read as a menu exit in every log.
		var sceneName = SceneManager.GetActiveScene().name;
		var pos = inWorld && _localBody != null // Unity object — ==
			? new NetVector2(_localBody.transform.position.x, _localBody.transform.position.y)
			: (NetVector2?)null;
		_session.ReportSceneState(inWorld ? SceneStateType.InWorld : SceneStateType.InMenu, sceneName, pos);
	}

	/// <summary>Re-publish the local body stream after a carried-follow placement; the stream must carry the rider's final visual position.</summary>
	internal void RefreshLocalBodyState()
	{
		if (_localBody != null) // Unity object — ==
		{
			PublishBodyState(_localBody);
		}
	}

	/// <summary>Guest side: the host released the start gate — start playing.</summary>
	private void OnRemoteWorldReady()
	{
		if (_descent is DescentPhase.Accepted or DescentPhase.Launched)
		{
			// The gate's 30 s force-start (or a host that finished first) can release before this
			// member's own generation settles: remember the release and go playing at the settle,
			// instead of leaving the member frozen on a gate that is no longer there.
			_descent = DescentPhase.ReleasedEarly;
			_log.LogInformation("[LayerDescent] the host released the gate while this member's new layer is still generating — it starts playing as soon as its own generation settles.");
			return;
		}

		if (_descent == DescentPhase.WaitingReady)
		{
			_descent = DescentPhase.None;
			_phase = RunPhase.Playing;
			_log.LogInformation("World ready — start playing (the descent's new layer was already generated).");
			return;
		}

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
		_hostDescentPending = false; // the gate released — the descent's presentation window closes with it
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

		var sceneName = SceneManager.GetActiveScene().name;
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
			if (_descent != DescentPhase.None)
			{
				_log.LogInformation("[LayerDescent] the host left the world — this is the descent this member is following, not a menu exit.");
				return;
			}

			_phase = RunPhase.Idle;
			_menuReturn.Request(_session.Role, RunMenuReturnOrigin.HostPull, _inWorld);
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
		_descent = DescentPhase.None;
		_descentBody.Clear();
		_hostDescentPending = false;
		_params.ResetSessionState();
		// The starting-supplies record is session-scoped: the bodies it holds belong to a world
		// this client is leaving, and a later run must judge its own. It is cleared here as well
		// as at a run start, because a session can end with the client still in the world (the
		// host leaving a lobby it stays in) and the next session's first body would otherwise be
		// compared against a body that is long gone.
		_startingSupplies.Clear();
		_menuReturn.Request(_session.Role, RunMenuReturnOrigin.SessionTeardown, _inWorld);
	}

	// ---- State shuttling ----

	private void PublishBodyState(Body body)
	{
		// Ragdoll/dead/unconscious: track the upper-torso world position, not
		// the Body transform. The visible limbs are not reliably centered on the
		// Body transform; KrokMP's inspected sync packet uses limbs[1] as the
		// ragdoll position anchor. Keeping the stream anchor on the torso also
		// makes the remote clone root/UI follow the collapsed body.
		// A CARRIED rider is the deliberate exception: it is placed by the
		// shared ride-pose path at the carrier's back using its body-root
		// transform, not a ragdoll. Publishing the torso anchor would make the
		// rider's own/carrier-side views agree with each other while every
		// third-party clone is offset differently — the reported vertical
		// placement asymmetry. Carried bodies always publish the body root.
		var upperTorso = !body.standing
			&& !CarriedBodyPose.ShouldPublishBodyRoot(CarriedBodyDriver.IsCarrying(body))
			&& body.limbs.Length > 1
			? body.limbs[1]
			: null;
		var pos = !body.standing && upperTorso != null // Unity object — ==
			? upperTorso.transform.position
			: body.transform.position;
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
		//   (private; sleeping is covered by the sleeping flag), and never while
		//   the local body is either half of a carry relation — the ride/carry
		//   presentation replaces the native sit state and must not leak onto
		//   the wire.
		// - sleeping: Body.cs:3961.
		// - climbing: currentClimbable (Body.cs:470).
		var isCarryParticipant = _playerInteraction.TryGetCarrier(_session.LocalSteamId, out _)
			|| _playerInteraction.TryGetCarried(_session.LocalSteamId, out _);
		var isCarried = CarriedBodyDriver.IsCarrying(body);
		var sitting = CarriedBodyPose.ShouldPublishSitting(
			isCarryParticipant,
			body.idleTime > 12f,
			body.exercising);
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
		// Nap variant: TakeANap selects the standard or sick/alt coroutine; the
		// tracker records the exact choice (0 = standard, 1 = alt). Forced sleep
		// (body.sleeping without a nap tracker) stays 0 = standard lay-down.
		var napVariant = body.sleeping
			&& body.TryGetComponent<LocalNapTracker>(out var napTracker)
				? napTracker.NapVariant
				: (byte)0;
		// Dog shake is a public continuous body field; the clone's HandleVisuals
		// reads it when BodyPatches runs the render-only path.
		var dogShakeIntensity = body.dogShakeIntensity;
		// Wall slide is continuous body state written by HandleGroundedState
		// from the local move input + side raycasts (Body.cs:2600-2601); the
		// render proxy needs the same flags to replay the Wall clip/particle.
		var slidingLeft = Traverse.Create(body).Field("slidingLeft").GetValue<bool>();
		var slidingRight = Traverse.Create(body).Field("slidingRight").GetValue<bool>();
		// Exact limb poses ride the same 20 Hz state stream while the body is
		// in a non-standing, non-sleeping lying pose (ragdoll/dead/unconscious).
		// The remote clone is frozen, so this is the only way its visible limbs
		// can match the owner's physics-driven pose. A carried rider is not a
		// ragdoll on the visual path: it must keep the carry mount/visual-standing
		// presentation so the carrier-side rider clone's limbs stay attached.
		var limbPoses = CarriedBodyPose.ShouldPublishExactLimbPoses(isCarried, body.alive, body.conscious)
			? LimbPoseCapture.Capture(body)
			: null;
		// The host clone's vitals-model inputs, read off the owner's own body (HostVitalsInputs owns
		// the walk threshold, the workout flag and the climb state; the receiver is the host pass).
		var vitals = HostVitalsInputs.FromBody(body);
		_entities.PublishLocalState(
			new NetVector2(pos.x, pos.y),
			new NetVector2(look.x, look.y),
			new NetVector2(vel.x, vel.y),
			body.isRight, body.standing, body.alive, body.conscious, body.crouching,
			lookOverridePos, body.overrideLookTime, body.eyeScareTime,
			body.eyePanicTime, body.eyeCloseTime,
			sitting, body.sleeping, body.currentClimbable != null, // Unity object — ==
			workoutType, napVariant, dogShakeIntensity,
			slidingLeft, slidingRight,
			limbPoses, vitals.InWater, vitals.Moving, vitals.Exercising);
	}
}
