using System;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.GameAdapter.World;
using Microsoft.Extensions.Logging;
using UnityEngine;

using System.Linq;

using HarmonyLib;

using Random = UnityEngine.Random; // System.Random vs UnityEngine.Random ambiguity — the game's jitter uses the Unity stream

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// Start-gate presentation: the "everyone loads together" wait — host forces
/// the gate after 30 s, both sides freeze the local player while it holds
/// (timeScale 0 + movingAllowed locked + the loading screen kept visible for
/// a guest) and restore everything on release. The gate's STATE lives in the
/// IWorldControl (host) and the RunCoordinator phase machine (guest); this
/// class only reads it and drives the presentation.
/// </summary>
internal sealed class StartGateCoordinator(
	ISessionControl session,
	IWorldControl world,
	LifePodPresentation lifePod,
	RunCoordinator run,
	ILogger<StartGateCoordinator> log)
{
	private readonly ISessionControl _session = session;
	private readonly IWorldControl _world = world;
	private readonly LifePodPresentation _lifePod = lifePod;
	private readonly RunCoordinator _run = run;
	private readonly ILogger<StartGateCoordinator> _log = log;

	/// <summary>The gate holds us: world timeScale=0 + movingAllowed locked — restore both on release.</summary>
	private bool _gateFrozen;

	/// <summary>Guest: when the WorldReady wait began (0 = not waiting) — safety-valve timeout.</summary>
	private long _worldReadyWaitMs;
	private const int WorldReadyTimeoutMs = 60_000; // guest: force back to the menu if the host never releases the gate

	/// <summary>Guest: the game's loading screen instance kept visible while the start gate holds (Unity object — == null when the scene switched).</summary>
	private GameObject? _keptLoadingObject;

	/// <summary>PlayerCamera.DoAlert popups suppressed while the start-gate window holds, replayed in order after the release.</summary>
	private readonly StartGateAlertQueue _alerts = new();

	/// <summary>
	/// Guest, at the generation boundary: arm the loading-screen keeper. The
	/// game hides its loading screen at generation end (WorldGeneration.cs:3637,
	/// after the fade-out); the keeper lives on the world object and re-shows it
	/// in LateUpdate — the same frame the coroutine hid it, before any render
	/// (an OnDisable-based undo is rejected by Unity: "GameObject is already
	/// being activated or deactivated").
	/// </summary>
	internal void AttachKeepLoading()
	{
		if (WorldGeneration.world == null) // Unity object — ==
		{
			return;
		}

		var keeper = WorldGeneration.world.gameObject.GetComponent<LoadingScreenKeeper>();
		if (keeper == null) // Unity object — ==
		{
			keeper = WorldGeneration.world.gameObject.AddComponent<LoadingScreenKeeper>(); // generic AddComponent lives on GameObject in Unity 5.6
		}

		// Keep from the moment the game hides its loading screen (generation
		// finished) until the run actually starts — the loading frame stays
		// continuously visible, no hide/re-show gap ("a flash before the frozen
		// frame appears"). The host's phase reaches Playing via
		// MarkPlayingForHost on gate release, so !IsPlaying is safe on both
		// roles (solo play has no session and never arms the keeper).
		keeper.ShouldKeep = () => _session.SessionActive && !_run.IsPlaying;
		keeper.Loading = () => HarmonyTraverse.ReadLoadingObject();
		keeper.OnFirstKeep = () => _log.LogInformation("[Gate] keeping the loading screen up while waiting for the host.");
	}

	internal bool WaitingForReady => _session.Role switch
	{
		SessionRole.Host => _world.StartGateActive,
		SessionRole.Guest => _run.GuestWaitingForReady,
		_ => false, // solo play — no session, no gate
	};

	/// <summary>
	/// Overlay text while the gate holds — the ONE line the player reads, worded from
	/// <see cref="WaitingState"/> so the screen and the log can never describe two
	/// different waits.
	/// </summary>
	internal string WaitingText => SessionWaitText.Line(WaitingState);

	/// <summary>
	/// What the session is waiting for, as one value. This getter only GATHERS the facts;
	/// which wait they add up to is <see cref="SessionWaitPolicy.Decide"/>, which is pure
	/// and therefore a case rather than an accident — the two claims a review caught this
	/// side making about other machines both lived here.
	/// </summary>
	internal SessionWaitState WaitingState => SessionWaitPolicy.Decide(new SessionWaitInput(
		_session.Role == SessionRole.Host,
		_session.LocalSteamId,
		_session.HostSteamId,
		[.. _session.Members.Select(member => new SessionWaitMember(member.SteamId, member.Handshaken, member.InWorld, member.DisplayName))],
		_session.IsRemoteInWorld(_session.HostSteamId),
		_world.StartGateRemainingMs / 1000,
		Environment.TickCount - (int)_run.HostInWorldSinceMs));

	/// <summary>
	/// PlayerCamera.DoAlert entry — true when the popup was queued instead of
	/// shown (the prefix returns false and skips the original). The window is
	/// RunCoordinator's start-gate alert span: the layer-title popup fires at
	/// generation end (WorldGeneration.cs:3640-3659), one frame BEFORE the
	/// world-entry edge arms the host's gate, so a plain "gate active" check
	/// would miss it.
	/// </summary>
	internal bool DeferAlert(string text, bool important)
	{
		if (!_run.IsStartGateAlertWindow)
		{
			return false;
		}

		_log.LogInformation("[Gate] deferring alert '{Alert}' until the gate release.", text);
		return _alerts.TryDefer(text, important);
	}

	/// <summary>Session ended — the queued popups belong to the dead run and must not replay into the next lobby.</summary>
	internal void ResetSessionState() => _alerts.Clear();

	/// <summary>
	/// Start-gate pump: the host forces the gate after 30 s (slow loaders
	/// finish on their own); both sides freeze the local player's movement
	/// while the gate holds and restore it on release.
	/// </summary>
	internal void Update(Body? localBody)
	{
		if (_session.Role == SessionRole.Host && _world.StartGateActive)
		{
			_world.MaybeForceStartGate();
		}

		ResumeHostPlayingIfTheSessionRoseAroundTheRun();

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

			if (localBody != null) // Unity object — ==
			{
				Traverse.Create(localBody).Field("movingAllowed").SetValue(false);
			}

			KeepLoadingScreenForGate(); // both roles: the loading screen stays up while the gate waits
			AnimateLoadingForGate(); // ... and keeps its loading jitter, so the wait reads as "still loading"

			if (_session.Role == SessionRole.Guest)
			{
				// Safety valve: the host may never release the gate (a start
				// the game refused after our entry hook, a dead process) —
				// leave back to the menu instead of freezing forever.
				if (_worldReadyWaitMs == 0)
				{
					_worldReadyWaitMs = Environment.TickCount;
				}
				else if (Environment.TickCount - _worldReadyWaitMs > WorldReadyTimeoutMs)
				{
					_worldReadyWaitMs = 0;
					_log.LogWarning("WorldReady never arrived within {Timeout}s — back to the menu.", WorldReadyTimeoutMs / 1000);
					if (PlayerCamera.main != null) // Unity object — ==
					{
						PlayerCamera.main.ToMainMenu();
					}
				}
			}
		}
		else if (_gateFrozen)
		{
			_gateFrozen = false;
			Time.timeScale = 1f;
			if (localBody != null) // Unity object — ==
			{
				Traverse.Create(localBody).Field("movingAllowed").SetValue(true);
			}

			_worldReadyWaitMs = 0;
			if (_session.Role == SessionRole.Host)
			{
				_run.MarkPlayingForHost(); // the keeper's !IsPlaying gate releases the loading screen (the host never gets a WorldReady for itself)
			}

			HideLoadingScreenForGate();
			_lifePod.Replay();
		}
		else if (_lifePod.HasDeferredEffects)
		{
			// Deferred but never held by a gate: the 30 s force-start released
			// the host BEFORE this side finished loading, so WorldReady arrived
			// first and WaitingForReady never armed — the release branch never
			// ran. Replay now that we are playing.
			_lifePod.Replay();
		}

		// Gate released (any path) — drop the kept loading screen (only the
		// instance we kept; a scene switch gives the game a fresh one).
		if (!WaitingForReady)
		{
			_worldReadyWaitMs = 0;
			if (_keptLoadingObject != null) // Unity object — ==
			{
				HideLoadingScreenForGate();
			}
		}

		// Deferred DoAlert popups (the layer title, its delayed description)
		// replay once the run is actually playing. A window that closed without
		// playing (world left, session ended) discards them instead.
		ReplayDeferredAlerts();
	}

	/// <summary>
	/// Host: a session can rise around a run that is ALREADY playing — the host played solo and
	/// opened the lobby from inside that world. The world-entry edge (where a fresh run marks itself
	/// playing and where the start gate arms) is long past, so neither
	/// <see cref="RunCoordinator.MarkPlayingForHost"/> call site can reach this run, and the keeper's
	/// <c>!IsPlaying</c> gate pulls the native loading screen up and pins it over the running game for
	/// the rest of the session (batch 20261021-e, session B: the host log shows the keep at
	/// 19:28:45.502 and no line ever releases it). The host's phase machine only has Idle and Playing
	/// — the follow phases belong to the guest — so <c>!IsPlaying</c> is this run's "not told yet". An
	/// armed gate keeps ownership of the phase: its release marks the host playing.
	/// </summary>
	private void ResumeHostPlayingIfTheSessionRoseAroundTheRun()
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld
			|| _run.IsPlaying || _world.StartGateActive)
		{
			return;
		}

		_log.LogInformation("The session rose around an already-running world — the host resumes playing.");
		_run.MarkPlayingForHost();
	}

	/// <summary>Replay the popups captured during the wait, in capture order. The
	/// original PlayerCamera.DoAlert now runs normally — the alert window is
	/// closed (IsPlaying), so the patch does not re-queue them.</summary>
	private void ReplayDeferredAlerts()
	{
		if (_run.IsStartGateAlertWindow || !_alerts.HasPending)
		{
			return;
		}

		var alerts = _alerts.TakeAll();
		if (!_run.IsPlaying || PlayerCamera.main == null) // Unity object — ==
		{
			_log.LogDebug("[Gate] dropped {Count} deferred alert(s) — the run is no longer playing.", alerts.Count);
			return;
		}

		foreach (var alert in alerts)
		{
			_log.LogInformation("[Gate] replaying deferred alert '{Alert}' (important {Important}).", alert.Text, alert.Important);
			PlayerCamera.main.DoAlert(alert.Text, alert.Important);
		}
	}

	/// <summary>
	/// Guest: the world is generated but the host has not released the gate —
	/// the game hides its loading screen at generation end (WorldGeneration.cs:
	/// 3637), which would show a frozen black world behind the wait overlay.
	/// Keep the loading screen up instead, so the wait reads as "still loading"
	/// (the LateUpdate keeper in AttachKeepLoading is the no-black-frame path;
	/// this per-frame re-show is the belt-and-suspenders backstop).
	/// </summary>
	private void KeepLoadingScreenForGate()
	{
		var loading = HarmonyTraverse.ReadLoadingObject();
		if (loading == null) // Unity object — ==
		{
			return;
		}

		_keptLoadingObject = loading; // Unity object — == (a scene switch replaces it; the old one reads destroyed)
		loading.SetActive(true);
		// The wait reads as "still loading": the game's own jitter animation
		// (the gen figures) is mirrored by AnimateLoadingForGate, which runs
		// with this keep. Pure presentation — the world stays frozen because
		// every scaled-time logic, coroutine and physics step is stopped by
		// timeScale 0.
	}

	/// <summary>
	/// First-layer loading jitter while the gate holds. The game only animates
	/// its gen figures while generatingWorld is true (WorldGeneration.cs:943-947:
	/// GenPod/GenCharacter jitter horizontally, the Image figure bobs vertically);
	/// once generation finishes the figures freeze forever. Mirror that jitter
	/// so the kept loading screen keeps reading as "still loading". Other layers
	/// are static during loading too (a fixed sit figure, WorldGeneration.cs:
	/// 960-961), so only the first layer animates — matching the loading phase.
	/// </summary>
	private void AnimateLoadingForGate()
	{
		if (HarmonyTraverse.ReadBiomeDepth() != 0 || HarmonyTraverse.ReadBiomeOverride() != 0)
		{
			return; // not the first layer — the loading phase is static here too (WorldGeneration.cs:939)
		}

		var genRects = HarmonyTraverse.ReadGenRects();
		if (genRects == null || genRects[1] == null || genRects[2] == null || genRects[4] == null) // Unity objects — ==
		{
			return;
		}

		var offset = new Vector2(Random.Range(-9f, 0f), 0f);
		genRects[1].anchoredPosition = offset; // GenPod
		genRects[2].anchoredPosition = offset; // GenCharacter
		genRects[4].anchoredPosition = new Vector2(0f, Random.Range(-genRects[4].sizeDelta.y * 0.4f, genRects[4].sizeDelta.y * 0.4f)); // Image
	}

	private void HideLoadingScreenForGate()
	{
		var loading = _keptLoadingObject; // Unity object — ==
		_keptLoadingObject = null;
		if (loading != null) // Unity object — ==
		{
			// The keeper's ShouldKeep reads Playing and stays quiet from now on.
			// (The animation is never disabled anymore — it just goes with the
			// screen. This keeps the symmetric close for whichever code armed
			// the screen: the keeper path and the gate path share this close.)
			loading.SetActive(false);
		}
	}
}
