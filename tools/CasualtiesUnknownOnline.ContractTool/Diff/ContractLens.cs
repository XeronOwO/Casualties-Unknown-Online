namespace CasualtiesUnknownOnline.ContractTool.Diff;

/// <summary>
/// How the patch-target contract rows resolved against one build. A contract
/// that was ALREADY unresolvable is not a difference between two builds, so the
/// diff cannot report it — this census is where it stays visible, and it is also
/// the "the tool looked at something" number a report quotes instead of a bare
/// silent verdict list.
/// </summary>
public sealed record ContractLens(int Resolved, int Ambiguous, int Unresolved)
{
	/// <summary>Every contract row the lens saw.</summary>
	public int Total => Resolved + Ambiguous + Unresolved;
}
