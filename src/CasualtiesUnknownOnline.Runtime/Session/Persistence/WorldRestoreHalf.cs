namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// One of the live-world writers a restore owes a contribution from
/// (<see cref="WorldRestoreAudit"/>). A restore arms its halves in two places in
/// time — the kernel restore at the Continue click and the generation reconcile —
/// and each of them can be released by several paths afterwards (the session
/// ending, a supersession, a layer-end cut, a throw at the seam). The half is the
/// IDENTITY that makes a contribution attributable: it says WHICH writer reported,
/// so a second release of the same arm cannot stand in for a half the restore still
/// owes, and a release that never reported cannot be silently substituted by
/// another half's report.
/// </summary>
public enum WorldRestoreHalf
{
	/// <summary>
	/// The Runtime world-fact tables (the block diff, the radiation line) together
	/// with the adapter's native handover (the decided keypad codes and geyser
	/// types, the game's own partial-damage list, the recipe unlock table). ONE
	/// half: the world-entry seam writes both in one call and reports them as one
	/// account, and it is the half every restore owes — a cut that carried no fact
	/// at all still reports it as "carried nothing to write".
	/// </summary>
	WorldFacts,

	/// <summary>The kernel's restored per-entity facts (consumed traps, opened lockables, building health), written at the world-entry seam after the world facts.</summary>
	WorldEntities,

	/// <summary>The restored world items, landed by the generation reconcile rather than by the world-entry seam.</summary>
	WorldItems,
}
