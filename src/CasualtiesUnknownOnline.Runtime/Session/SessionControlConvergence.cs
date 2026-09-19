using System;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Guest-side readiness convergence (sync-coverage audit rows R3/R4). The session
/// control messages are one-shot over a reliable transport, but the transport's
/// retry only covers a lost frame while the peer is reachable — the documented
/// lazy-P2P swallow window drops frames the sender never learns about, and three of
/// them had no re-report at all: a swallowed <c>SceneState</c> report left the host
/// without the member's InWorld edge (no world-entry fan-out, no entity sync, the
/// guest's own 60 s start-gate valve back to the menu as the only escape), a
/// swallowed <c>WorldReady</c> left the guest waiting at the gate although the host
/// had released it, and a swallowed <c>HandshakeAckAck</c> left the host's member
/// unconfirmed for the whole connection (that half lives in
/// <see cref="SessionPeerMaintenance"/> and the handshake handlers).
///
/// The fix is the same absolute-fact pattern the world tables use: the guest
/// re-asserts its ABSOLUTE scene report in a bounded window after each scene edge,
/// and stops as soon as the facts the host's answer carries are in. For an entry
/// those facts are the world-entry group's completion marker (only the host sends it
/// after it has run the fan-out for this member) and the start-gate release; the
/// host answers a repeat report with exactly those two and never re-runs the fan-out
/// (<see cref="Handlers.SceneStateHandler"/>), so a repeat is idempotent.
///
/// The EXIT edge (leaving the world) is re-asserted too, because the host's member
/// table drives the entity sync and the render clones: a swallowed InMenu report used
/// to leave the host holding the member in world for the rest of the session. No
/// answer exists for that direction (nothing the host sends acknowledges "I saw you
/// leave"), so its window is a plain bounded repeat: <see cref="MaxExitReports"/>
/// covers the swallow window and then stops — acceptance by design, logged, never
/// silently carried.
///
/// The entry window's budget must OUTLAST the host's own start gate: the release can
/// legitimately happen up to the gate's 30 s force-start after the host armed it, and
/// the release broadcast is itself a one-shot — a budget shorter than that would stop
/// re-asserting before the very message it is meant to heal could be lost, which is
/// exactly the failure the window exists to prevent. Hence <see cref="MaxReports"/>
/// re-reports at <see cref="IntervalMs"/> (60 s, the guest's own valve timescale), and
/// a member the host still never answered is NAMED in a warning instead of trickling
/// forever. A repeat counts against that budget only when it actually left the client
/// (<see cref="ISessionControl.ResendSceneState"/> reports whether it did), so a
/// session that is not active cannot spend the window on reports that never went out.
/// </summary>
internal sealed class SessionControlConvergence : ICuoService
{
	/// <summary>The re-report cadence — well inside the guest's 60 s start-gate valve.</summary>
	internal const long IntervalMs = 5_000;

	/// <summary>Entry-window reports (12 × 5 s = 60 s): covers the host's 30 s gate force-start plus the swallow window on top of it.</summary>
	internal const int MaxReports = 12;

	/// <summary>Exit-window reports (6 × 5 s = 30 s): the documented swallow window; no answer exists to wait for.</summary>
	internal const int MaxExitReports = 6;

	private readonly ISessionControl _session;
	private readonly IWorldControl _world;
	private readonly ITimeSource _time;
	private readonly ILogger<SessionControlConvergence> _log;

	/// <summary>Host answered the entry: the completion marker arrived (the fan-out ran for this member).</summary>
	private bool _entryGroupComplete;

	/// <summary>Host released the start gate for this member (WorldReady arrived).</summary>
	private bool _gateReleased;

	/// <summary>
	/// The scene state this window belongs to — set by the report edge
	/// (<see cref="ISessionControl.LocalSceneReported"/>), never polled: the report that opens
	/// the window is a synchronous local write, and an edge that came and went between two
	/// updates would otherwise be missed.
	/// </summary>
	private bool _inWorld;

	/// <summary>The window is open (a re-report is still allowed for this scene edge).</summary>
	private bool _armed;

	/// <summary>When the current cadence window started (or the last re-report went out).</summary>
	private long _windowMs;

	private int _reports;

	public SessionControlConvergence(ISessionControl session, IWorldControl world, ITimeSource time, ILogger<SessionControlConvergence> log)
	{
		_session = session;
		_world = world;
		_time = time;
		_log = log;
		_world.WorldReadyReceived += OnGateReleased;
		_world.WorldSnapshotCompleteReceived += OnEntryGroupComplete;
		_session.LocalSceneReported += OnLocalSceneReported;
		_session.SessionEnded += OnSessionEnded;
	}

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Stop()
	{
	}

	void IDisposable.Dispose()
	{
		_world.WorldReadyReceived -= OnGateReleased;
		_world.WorldSnapshotCompleteReceived -= OnEntryGroupComplete;
		_session.LocalSceneReported -= OnLocalSceneReported;
		_session.SessionEnded -= OnSessionEnded;
	}

	void ICuoService.Update()
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			// Not our session (host/solo) or not established yet: drop the window and its
			// facts, so the next report starts from a clean edge.
			ResetWindow();
			return;
		}

		if (!_armed)
		{
			return; // this window is spent — the adapter's valve stays the last resort
		}

		var nowMs = _time.NowMs;
		if (_inWorld && _entryGroupComplete && _gateReleased)
		{
			if (_reports > 0)
			{
				_log.LogInformation("Session control converged after {Reports} scene re-report(s).", _reports);
			}

			_armed = false;
			return;
		}

		if (nowMs < _windowMs)
		{
			// The clock went backwards (Environment.TickCount wraps every ~24.9 days):
			// restart the window at the new reading instead of stalling on the old one.
			_windowMs = nowMs;
			return;
		}

		if (nowMs - _windowMs < IntervalMs)
		{
			return;
		}

		var budget = _inWorld ? MaxReports : MaxExitReports;
		if (_reports >= budget)
		{
			_armed = false;
			if (_inWorld)
			{
				_log.LogWarning("Scene report was not answered after {Reports} re-reports — still missing {Missing}. The 60 s start-gate valve stays the last resort.",
					_reports, Missing());
			}
			else
			{
				// The exit direction has no answer to wait for: the budget IS the policy. The
				// wording matters — a swallowed exit is exactly what leaves the host holding
				// the member IN world.
				_log.LogInformation("Exit report re-asserted {Reports} time(s); the host keeps the member IN world until one arrives.", _reports);
			}

			return;
		}

		_windowMs = nowMs;
		if (!_session.ResendSceneState())
		{
			return; // nothing left the client (session not active) — a report that never went out must not spend the window
		}

		_reports++;
		if (_inWorld)
		{
			_log.LogInformation("Re-reported the scene state to the host ({Reports}/{Max}) — still missing {Missing}.",
				_reports, budget, Missing());
		}
		else
		{
			_log.LogInformation("Re-reported the world exit to the host ({Reports}/{Max}).", _reports, budget);
		}
	}

	/// <summary>
	/// A local scene report was made (either direction): open a window for it. Armed here
	/// rather than by polling the scene flag, so the edge cannot be missed between two
	/// updates and a response delivered inside the report call still lands on an armed
	/// window. The facts of the previous window are dropped with it — a stale marker or
	/// release must never satisfy the next edge.
	/// </summary>
	private void OnLocalSceneReported(SceneStateType state)
	{
		_inWorld = state == SceneStateType.InWorld;
		_armed = true;
		_reports = 0;
		_windowMs = _time.NowMs;
		_entryGroupComplete = false;
		_gateReleased = false;
	}

	/// <summary>The session is gone (or this node is not its guest) — drop the window and its facts.</summary>
	private void ResetWindow()
	{
		if (!_inWorld && !_armed && !_entryGroupComplete && !_gateReleased)
		{
			return;
		}

		_inWorld = false;
		_armed = false;
		_reports = 0;
		_entryGroupComplete = false;
		_gateReleased = false;
	}

	private void OnGateReleased() => _gateReleased = true;

	private void OnEntryGroupComplete() => _entryGroupComplete = true;

	private void OnSessionEnded() => ResetWindow();

	private string Missing()
	{
		if (!_entryGroupComplete && !_gateReleased)
		{
			return "the entry-group completion marker and the start-gate release";
		}

		return _entryGroupComplete
			? "the start-gate release (WorldReady)"
			: "the entry-group completion marker (WorldSnapshotComplete)";
	}
}
