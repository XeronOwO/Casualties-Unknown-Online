using System;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The generic pending-report fallback's window: an unacknowledged local report
/// whose live send was swallowed is re-sent every <see cref="IntervalMs"/>
/// until the owner answers. The window is armed by the first outstanding entry,
/// so the first live report is never duplicated immediately (an entry created
/// while the window is open is re-sent at the window's end — every pending
/// report is idempotent on the receiver). Entries are dropped by the owning
/// domain when the answer arrives, and cleared wholesale when a new world/layer
/// baseline is applied. <see cref="WorldReportFallbackPump"/> is the clock;
/// this class is the cadence policy shared by every pending-report table
/// (world blocks — audit gap W1 — and runtime entity creations — audit gap E3).
/// </summary>
internal sealed class PendingReportFallback(ISessionControl session)
{
	/// <summary>The re-report cadence — the same 60 s family as the host's absolute world resends.</summary>
	internal const long IntervalMs = 60_000;

	private readonly ISessionControl _session = session;

	private long _armedMs;
	private bool _armed;

	/// <summary>
	/// One frame of the cadence: arm on the first outstanding entry, re-send
	/// once per window while entries remain. <paramref name="pendingCount"/> is
	/// the owning table's live count and <paramref name="resend"/> its re-send
	/// action (a no-op when the table emptied between the check and the call).
	/// </summary>
	internal void Pump(long nowMs, int pendingCount, Action resend)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive || pendingCount == 0)
		{
			_armed = false;
			return;
		}

		if (!_armed)
		{
			_armed = true;
			_armedMs = nowMs;
			return;
		}

		if (nowMs < _armedMs)
		{
			// The clock went backwards (Environment.TickCount wraps every ~24.9
			// days) — re-arm at the new reading instead of stalling until the
			// counter catches up with the old one.
			_armedMs = nowMs;
			return;
		}

		if (nowMs - _armedMs < IntervalMs)
		{
			return;
		}

		_armedMs = nowMs;
		resend();
	}

	/// <summary>The session ended — the next session's first report must start a fresh window.</summary>
	internal void Reset()
	{
		_armed = false;
		_armedMs = 0;
	}
}
