namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// When a restore holds the native geyser set and when it gives up. The live set is
/// not there at the world-entry seam: the geysers are instantiated while the layer
/// fills in, and the count keeps growing for seconds. Measured 2026-11-17: one world
/// stood at 94 of 114 objects five seconds past the seam, another reached 126 of 126
/// nine seconds in — a fixed window either refuses rows the world is about to have or
/// parks a genuinely short cut forever.
///
/// The rule is the SHAPE of the fill, not a number: hold while the count grows, release
/// once it has stood still for <see cref="SettleSeconds"/>, and never hold past
/// <see cref="CeilingSeconds"/> — a set that flatlines must be reported, not awaited.
/// </summary>
internal static class GeyserFillWait
{
	/// <summary>How long the live count may stand still before the set counts as complete.</summary>
	internal const double SettleSeconds = 3;

	/// <summary>The longest a still-growing set may keep the restore open.</summary>
	internal const double CeilingSeconds = 30;

	/// <summary>True while the restore should wait for more geysers to appear.</summary>
	internal static bool Holds(int live, int saved, double stableSeconds, double elapsedSeconds) =>
		live < saved
		&& stableSeconds < SettleSeconds
		&& elapsedSeconds < CeilingSeconds;
}
