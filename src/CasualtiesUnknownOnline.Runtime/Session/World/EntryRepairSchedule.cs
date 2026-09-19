namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Host-side cadence for the world-entry repair (sync-coverage audit rows R3/W7,
/// cadence review <c>review/sync-cadence-review.md</c>). The entry group and the
/// tables the host re-sends on a member's InWorld edge are one-shot over a
/// reliable transport whose retry only covers a lost frame while the peer is
/// reachable: the documented lazy-P2P swallow window drops everything sent
/// before the session is up, and the next absolute send used to be the host's
/// 60 s repair cycle.
///
/// <para>
/// The heal rides the guest's own readiness window instead of a blind timer:
/// <see cref="SessionControlConvergence"/> re-asserts the member's scene
/// report every 5 s until the entry-group completion marker AND the start-gate
/// release are both in, so a REPEAT report states exactly two facts — "my entry
/// answer did not complete me" and "my uplink is up now". The first repeat is
/// therefore answered with the entry state it may have missed, and a window that
/// stays open keeps being answered at <see cref="RepairIntervalMs"/>: bounded by
/// the guest's own 60 s window at six repairs, never a trickle. A clean entry
/// (both control facts arrived) never repeats and never pays for a repair.
/// </para>
///
/// <para>
/// A repeat does not say WHICH fact is missing, so a window held open by a slow
/// start gate (a legitimately armed gate re-asserts until its release) also
/// receives the repair; that is one bounded, idempotent pass per repair interval
/// whose cost is measured in <c>docs/evidence/sync-cadence-measurements.md</c>.
/// </para>
/// </summary>
public sealed class EntryRepairSchedule
{
	/// <summary>The repair cadence while the member's entry window is still re-asserting: the first repeat repairs at once, later repeats follow at this interval (six repairs inside the guest's 12 × 5 s window).</summary>
	internal const long RepairIntervalMs = 10_000;

	private long _lastRepairMs;
	private int _repairs;
	private bool _repaired;

	/// <summary>How many repairs this entry has been answered with (the log line's count).</summary>
	internal int Repairs => _repairs;

	/// <summary>A member entered the world: this entry's repair state starts from zero.</summary>
	public void Arm()
	{
		_repaired = false;
		_repairs = 0;
	}

	/// <summary>
	/// Whether this repeat report should be answered with the entry state.
	/// </summary>
	public bool TryClaim(long nowMs)
	{
		// A clock that went backwards (Environment.TickCount wraps every ~24.9
		// days) counts as elapsed: re-basing on the new reading beats refusing to
		// repair for the rest of the wrap.
		if (_repaired && nowMs >= _lastRepairMs && nowMs - _lastRepairMs < RepairIntervalMs)
		{
			return false;
		}

		_repaired = true;
		_lastRepairMs = nowMs;
		_repairs++;
		return true;
	}
}
