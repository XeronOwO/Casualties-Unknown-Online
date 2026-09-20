namespace CasualtiesUnknownOnline.ContractTool.Diff;

/// <summary>
/// How far a difference sits from CUO's hooks — the report's tiers, and the
/// reason a game update produces a short actionable list instead of a changelog:
///   - <see cref="Contract"/>: the difference IS a verdict on a patch-target
///     contract row (the hook's own target moved, or the target is structurally
///     unchanged and needs the live half);
///   - <see cref="ContractAdjacent"/>: a member of a type some contract targets
///     changed without being a target itself (a field the patch reads, an enum
///     table the domain keys on);
///   - <see cref="OutsideContract"/>: a real change the contract lens does not
///     touch — recorded and counted so nothing is hidden, not turned into hook
///     work it is not.
/// </summary>
public enum DifferenceScope
{
	/// <summary>A verdict on a patch-target contract row.</summary>
	Contract,

	/// <summary>A change to a member of a type that some contract targets.</summary>
	ContractAdjacent,

	/// <summary>A change outside every contract's orbit.</summary>
	OutsideContract,
}
