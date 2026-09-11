namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// What one restore's world-fact apply actually landed. The block difference
/// table is BOUNDED, so a restore can put back fewer rows than the cut carried —
/// and §6 forbids a restore that reports success while a row was dropped. The
/// counts are therefore returned to the caller and folded into the restore
/// report instead of only reaching the log. The GAME's own tables report
/// separately: <see cref="INativeWorldFacts"/> hands its rows to an applier whose
/// own account carries what its caps refused.
/// </summary>
/// <param name="BlockStatesApplied">Block-difference rows the table accepted.</param>
/// <param name="BlockStatesRefused">Block-difference rows the table's cap refused.</param>
/// <param name="RadiationLineApplied">True when the cut carried a radiation line and it was put back.</param>
public readonly record struct WorldFactApplyReport(
	int BlockStatesApplied,
	int BlockStatesRefused,
	bool RadiationLineApplied)
{
	/// <summary>Rows of the bounded table that did NOT reach it.</summary>
	public int Refused => BlockStatesRefused;

	/// <summary>The refused rows as a restore-report fragment, or null when every row landed.</summary>
	public string? Describe() => Refused == 0
		? null
		: $"{BlockStatesRefused} restored block-state row(s) did not land in the table "
			+ $"({BlockStatesApplied} block-state row(s) applied)";
}
