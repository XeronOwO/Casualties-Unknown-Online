namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The guest block-report fallback's window (sync-coverage audit W1): a guest
/// block mutation whose live report was swallowed is re-reported every
/// <see cref="IntervalMs"/> until the host answers for that cell. The window is
/// armed by the first outstanding entry, so the first live report is never
/// duplicated immediately (an entry created while the window is open is
/// re-reported at the window's end — the host's arbitration is idempotent).
/// Entries are dropped by <see cref="WorldStateMessageService"/> when the
/// host's relay or correction answers the cell, and cleared wholesale when a
/// new world/layer baseline is applied. <see cref="BlockReportFallbackPump"/>
/// is the clock; this class is the cadence policy.
/// </summary>
internal sealed class BlockReportFallback(ISessionControl session, WorldStateMessageService messages)
{
	/// <summary>The guest re-report cadence — the same 60 s family as the host's absolute block-state resend.</summary>
	internal const long IntervalMs = 60_000;

	private readonly ISessionControl _session = session;
	private readonly WorldStateMessageService _messages = messages;

	private long _armedMs;
	private bool _armed;

	/// <summary>One frame of the cadence: arm on the first outstanding entry, re-report once per window while entries remain.</summary>
	internal void Pump(long nowMs)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive || _messages.PendingBlockReportCount == 0)
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
		_messages.ResendPendingBlockReports();
	}

	/// <summary>The session ended — the next session's first report must start a fresh window.</summary>
	internal void Reset()
	{
		_armed = false;
		_armedMs = 0;
	}
}
