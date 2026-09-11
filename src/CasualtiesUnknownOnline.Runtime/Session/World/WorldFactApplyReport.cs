namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// What one restore's world-fact apply actually landed. Both Runtime-owned
/// tables are BOUNDED (CUO's block difference table and its partial-damage
/// registry), so a restore can put back fewer rows than the cut carried — and
/// §6 forbids a restore that reports success while a row was dropped. The
/// counts are therefore returned to the caller and folded into the restore
/// report instead of only reaching the log.
/// </summary>
/// <param name="BlockStatesApplied">Block-difference rows the table accepted.</param>
/// <param name="BlockStatesRefused">Block-difference rows the table's cap refused.</param>
/// <param name="BlockDamagesApplied">Partial-damage rows the registry accepted.</param>
/// <param name="BlockDamagesRefused">Partial-damage rows the registry did not hold — its cap refused them, or the row was not a record at all (a non-positive damage).</param>
/// <param name="RadiationLineApplied">True when the cut carried a radiation line and it was put back.</param>
public readonly record struct WorldFactApplyReport(
	int BlockStatesApplied,
	int BlockStatesRefused,
	int BlockDamagesApplied,
	int BlockDamagesRefused,
	bool RadiationLineApplied)
{
	/// <summary>Rows of the two bounded tables that did NOT reach them.</summary>
	public int Refused => BlockStatesRefused + BlockDamagesRefused;

	/// <summary>The refused rows as a restore-report fragment, or null when every row landed.</summary>
	public string? Describe() => Refused == 0
		? null
		: $"{BlockStatesRefused} restored block-state row(s) and {BlockDamagesRefused} restored partial block-damage row(s) did not land in their tables "
			+ $"({BlockStatesApplied} block-state and {BlockDamagesApplied} partial-damage row(s) applied)";
}
