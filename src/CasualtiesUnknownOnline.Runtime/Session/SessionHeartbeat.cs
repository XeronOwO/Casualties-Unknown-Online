using System;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The session's ping schedule. It is deliberately independent of the world:
/// a session that is holding a transition (leave → menu → start) still owns
/// its peers, and a guest that hears nothing for three intervals ends the
/// session locally (<c>GuestHostSilenceWatchdog</c>). Measured 2026-11-14:
/// joining a session before the run left the guest without a frame for 15 s
/// and it left the session.
/// </summary>
/// <remarks>
/// The schedule starts one interval in: a session that has just been created
/// owes its peers nothing yet, and a measurement that counts the frames of an
/// entry window must not see a ping on its first tick. The ping itself stays
/// in <c>SessionService.RequestPing</c> — this type only owns when.
/// </remarks>
internal sealed class SessionHeartbeat(Func<long> nowMs, Action ping)
{
	private const int IntervalMs = 5000;

	private readonly Func<long> _nowMs = nowMs ?? throw new ArgumentNullException(nameof(nowMs));
	private readonly Action _ping = ping ?? throw new ArgumentNullException(nameof(ping));

	private long _nextPingMs;

	/// <summary>Runs the schedule: the first tick waits one interval, then each interval pings.</summary>
	public void Tick()
	{
		var now = _nowMs();
		if (_nextPingMs == 0)
		{
			_nextPingMs = now + IntervalMs;
			return;
		}

		if (now < _nextPingMs)
		{
			return;
		}

		_nextPingMs = now + IntervalMs;
		_ping();
	}
}
