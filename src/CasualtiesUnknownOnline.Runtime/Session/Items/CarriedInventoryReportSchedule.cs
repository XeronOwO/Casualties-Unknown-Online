namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Guest-side cadence for the absolute carried-inventory registration
/// (sync-coverage audit row I8). <c>CarriedInventory</c> is a REPORT of ids the
/// guest already assigned locally, and the transport is only trustworthy while
/// the peer is reachable: the documented lazy-P2P swallow window (up to ~30 s
/// after world entry) can drop the single registration frame without either side
/// noticing — the guest's later reports heal the kernel state accepted-first,
/// but the host's arbitration record stays empty for those ids.
///
/// <para>
/// The fix follows the family's absolute-report pattern: the guest re-reports the
/// CURRENT carried set (re-captured at every report, never a frozen frame — an
/// item the guest destroyed, dropped or handed over is simply absent from the
/// next capture, so a repeat cannot resurrect it) on a cadence that opens dense
/// and settles low. A registration WINDOW opens on every edge that makes a
/// registration meaningful — the local generation finished, or the host granted
/// the id watermark (the join/reconnect signal) — and reports
/// <see cref="BurstReports"/> times at <see cref="BurstIntervalMs"/> (12 x 5 s
/// ≈ 60 s, covering the entry swallow window at the same order of magnitude as
/// the item-command window and the host's own 60 s repair cycle), then keeps
/// re-reporting at <see cref="SteadyIntervalMs"/> for the rest of the world's
/// life.
/// </para>
///
/// <para>
/// The steady half is what makes the registration durable rather than one-shot:
/// an id the guest self-assigns LATER (a crafted product, an item unloaded from a
/// container) is covered by the next report, and so is a registration lost long
/// after entry. The host applies a report registration-only, so the repeats are
/// no-ops there — the steady cost is at most one carried-inventory frame per guest
/// per minute, and only when the capture has something to state (a step spent on an
/// empty capture sends nothing), against a 1 Hz character snapshot.
/// </para>
/// </summary>
public sealed class CarriedInventoryReportSchedule
{
	/// <summary>The window's dense cadence: the first repeat goes out one interval after the window opened.</summary>
	internal const long BurstIntervalMs = 5_000;

	/// <summary>Dense reports per window (12 x 5 s ≈ 60 s — the entry swallow window, the family's order of magnitude).</summary>
	internal const int BurstReports = 12;

	/// <summary>The steady cadence after the window: durable state is re-asserted once a minute, like the host's own absolute cycles.</summary>
	internal const long SteadyIntervalMs = 60_000;

	private long _nextDueMs;
	private long _lastMs;
	private int _burstRemaining;
	private bool _armed;

	/// <summary>How many dense reports are left in the current window (the caller's "first report of the window" test).</summary>
	internal int BurstRemaining => _burstRemaining;

	/// <summary>Open (or re-open) a registration window: the next report is due at once, and the dense cadence starts again.</summary>
	public void Arm(long nowMs)
	{
		_armed = true;
		_burstRemaining = BurstReports;
		_lastMs = nowMs;
		_nextDueMs = nowMs;
	}

	/// <summary>Whether a registration report is due at this reading.</summary>
	public bool IsDue(long nowMs)
	{
		if (!_armed)
		{
			return false;
		}

		if (nowMs < _lastMs)
		{
			// The clock went backwards (Environment.TickCount wraps every ~24.9
			// days) — re-base on the new reading instead of stalling until it
			// catches up to the old one.
			_lastMs = nowMs;
			_nextDueMs = nowMs;
			return true;
		}

		return nowMs >= _nextDueMs;
	}

	/// <summary>
	/// One step of the window was spent: a report went out, or the capture held
	/// nothing to register (an empty capture still spends the step, so a guest
	/// with nothing to report does not re-enumerate its body every frame).
	/// </summary>
	public void MarkReported(long nowMs)
	{
		if (_burstRemaining > 0)
		{
			_burstRemaining--;
		}

		_lastMs = nowMs;
		_nextDueMs = nowMs + (_burstRemaining > 0 ? BurstIntervalMs : SteadyIntervalMs);
	}

	/// <summary>Close the window (the session ended): nothing is due until the next edge opens one.</summary>
	public void Reset()
	{
		_armed = false;
		_burstRemaining = 0;
		_nextDueMs = 0;
		_lastMs = 0;
	}
}
