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

			if (_session.Role == SessionRole.Guest)
			{
				// The game hides its loading screen at generation end and the
				// world is live underneath — we deliberately do NOT keep it:
				// the screen is a full-black panel, keeping it up is what read
				// as "the black-screen wait" (the wait overlay text draws over
				// the live frozen world instead).
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
	}
}
