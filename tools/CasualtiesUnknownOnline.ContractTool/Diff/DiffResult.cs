using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.ContractTool.Snapshot;

namespace CasualtiesUnknownOnline.ContractTool.Diff;

/// <summary>
/// The outcome of comparing two snapshots: the two builds it compared, the
/// classified differences (sorted into report order), and the contract census of
/// each side.
/// </summary>
public sealed record DiffResult(
	SnapshotDocument Previous,
	SnapshotDocument Current,
	IReadOnlyList<ContractDifference> Differences,
	ContractLens PreviousLens,
	ContractLens CurrentLens)
{
	/// <summary>The differences in one report tier.</summary>
	public IEnumerable<ContractDifference> InScope(DifferenceScope scope) => Differences.Where(difference => difference.Scope == scope);

	/// <summary>True when a contract verdict says a hook's target moved — what <c>--fail-on-broken</c> keys on.</summary>
	public bool HasBrokenContracts => Differences.Any(difference =>
		difference.Scope == DifferenceScope.Contract && difference.Kind != DifferenceKind.UnchangedNeedsReview);

	/// <summary>How many differences of a kind the comparison found.</summary>
	public int Count(DifferenceKind kind) => Differences.Count(difference => difference.Kind == kind);
}
