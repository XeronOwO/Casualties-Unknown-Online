using System;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;

using System.Linq;

using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Start-gate presentation: the "everyone loads together" wait — host forces
/// the gate after 30 s, both sides freeze the local player while it holds
/// (timeScale 0 + movingAllowed locked + the loading screen kept visible for
/// a guest) and restore everything on release. The gate's STATE lives in the
/// WorldService (host) and the RunCoordinator phase machine (guest); this
/// class only reads it and drives the presentation.
/// </summary>
internal sealed class StartGateCoordinator(
	SessionService session,
	WorldService world,
	LifePodPresentation lifePod,
	RunCoordinator run,
	ILogger<StartGateCoordinator> log)
{
	private readonly SessionService _session = session;
	private readonly WorldService _world = world;
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

		// Keep while the gate itself holds — NOT !IsPlaying: the host never
		// receives a WorldReady for itself, so its run phase never reaches
		// Playing and a !IsPlaying-based keep would re-show the loading screen
		// forever over a running game ("the loading screen stays glued on top,
		// the player can already act").
		keeper.ShouldKeep = () => WaitingForReady;
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
	/// Overlay text while the gate holds: who we are waiting for and the
	/// force-start countdown. Host counts against the real gate (armed at its
	/// world entry); guest counts 30 s from the host's InWorld relay (network
	/// delay approximation of the host's own gate).
	/// </summary>
	internal string WaitingText
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
				var remaining = Mathf.Max(0, (int)(30 - (Environment.TickCount - _run.HostInWorldSinceMs) / 1000d));
				return $"Waiting for {others} player(s)… ({remaining}s)";
			}

			return "Starting…";
		}
	}

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

			KeepLoadingScreenForGate(); // both roles: the loading animation keeps playing while the gate waits

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
		// The wait reads as a frozen moment, not a running animation: the
		// ImageCycle sprite loop (unscaledDeltaTime — timeScale 0 does NOT stop
		// it) is disabled while the gate holds.
		foreach (var cycle in loading.GetComponentsInChildren<ImageCycle>(true))
		{
			cycle.enabled = false; // Unity object — == (GetComponentsInChildren on a destroyed parent)
		}
	}

	private void HideLoadingScreenForGate()
	{
		var loading = _keptLoadingObject; // Unity object — ==
		_keptLoadingObject = null;
		if (loading != null) // Unity object — ==
		{
			foreach (var cycle in loading.GetComponentsInChildren<ImageCycle>(true))
			{
				cycle.enabled = true; // the animation resumes with the game
			}

			loading.SetActive(false); // the keeper's ShouldKeep reads Playing and stays quiet from now on
		}
	}
}
