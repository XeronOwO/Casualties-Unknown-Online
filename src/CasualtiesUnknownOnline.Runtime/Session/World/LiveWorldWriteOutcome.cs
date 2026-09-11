namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// What one write into the live world actually landed. A restored cut names a
/// BOUNDED set of facts; the live world's tables are bounded too (the game's own
/// partial-damage list caps at 128, <c>WorldGeneration.cs:732-737</c>), and §6
/// forbids a restore that reports success while a row was dropped. The refused
/// count is returned so the replay can name an incomplete restore instead of
/// leaving the drop in the log alone.
/// </summary>
/// <param name="Applied">Rows this write created or updated.</param>
/// <param name="Refused">Rows this write did not take.</param>
internal readonly record struct LiveWorldWriteOutcome(int Applied, int Refused)
{
	/// <summary>Every row landed — the expected outcome of a restored replay.</summary>
	internal static LiveWorldWriteOutcome All(int applied) => new(applied, 0);
}
