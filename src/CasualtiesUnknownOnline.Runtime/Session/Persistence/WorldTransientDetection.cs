namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// Whether CUO can COUNT a policy row at the cut instant. It exists because the
/// ticket's rule is "a dropped transient must be logged with what was dropped and
/// why", and two very different situations can look alike in the table:
///
/// - <see cref="Observed"/>: a CUO owner reports the class at the cut (the Runtime
///   probe or the adapter's seam half), so a cut names it WITH its count.
/// - <see cref="Standing"/>: the state lives in the game and CUO has no counter
///   for it (the crafting coroutine, item velocity, the run clock, the physics
///   timers). Every cut drops it, so the report names the class WITHOUT a count —
///   claiming "nothing was in flight" would be the silent loss this table exists
///   to prevent. A later stage that gains an observer moves its row to
///   <see cref="Observed"/> and gets the count for free.
/// </summary>
public enum WorldTransientDetection
{
	/// <summary>A CUO owner reports this class at the cut instant.</summary>
	Observed,

	/// <summary>The game owns the state; CUO cannot count it, and every cut says it does not carry it.</summary>
	Standing,
}
