namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// When a restore holds the native geyser set and when it gives up. The live set is
/// not there at the world-entry seam: the geysers are instantiated while the layer
/// fills in, and the count keeps growing for seconds. Measured 2026-11-17: one world
/// stood at 94 of 114 objects five seconds past the seam, another reached 126 of 126
/// nine seconds in — a fixed window either refuses rows the world is about to have or
/// parks a genuinely short cut forever.
///
/// The rule is the COUNT, not the shape: the restore holds while the live set is short of the
/// archive's rows and releases at <see cref="CeilingSeconds"/> — a set that flatlines must be
/// reported, not awaited, and a short set is still worth waiting for while the layer fills in.
/// </summary>
internal static class GeyserFillWait
{
	/// <summary>The longest a still-growing set may keep the restore open.</summary>
	internal const double CeilingSeconds = 30;

	/// <summary>
	/// The ceiling for one archive: the fixed 30 s plus a tenth of a second per saved row, capped at
	/// <see cref="HardCeilingSeconds"/>. Measured 2026-11-25 in a long run: a 115-row archive was still
	/// 107 rows short at the flat 30 s, because the batch's chunk bursts are slower than a single row's;
	/// a per-row allowance costs a short archive nothing and stops a big one from being refused by a
	/// timeout that was set for a small one.
	/// </summary>
	internal static double CeilingFor(int saved) =>
		Math.Min(HardCeilingSeconds, CeilingSeconds + (saved * 0.1));

	/// <summary>The most any single restore may wait, so a broken fill is still reported.</summary>
	internal const double HardCeilingSeconds = 60;

	/// <summary>
	/// True while the restore should wait for more geysers to appear.
	///
	/// A set that stands still SHORT of the archive keeps the restore open until the ceiling:
	/// measured 2026-11-25 in a long acceptance run, the live count paused for three seconds
	/// between chunk bursts with 96 of 115 objects in place, the old rule read that pause as
	/// completion, and the restore then refused 111 rows — the run's world was complete and the
	/// restore was not. A short set is bounded by <see cref="CeilingFor"/> instead, and what
	/// it could not take is named in the audit line either way.
	/// </summary>
	internal static bool Holds(int live, int saved, double stableSeconds, double elapsedSeconds) =>
		live < saved && elapsedSeconds < CeilingFor(saved);
}
