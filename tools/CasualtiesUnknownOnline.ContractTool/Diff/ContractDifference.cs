namespace CasualtiesUnknownOnline.ContractTool.Diff;

/// <summary>
/// One classified difference. <see cref="Subject"/> is what a reader acts on
/// (a type, a member, or a contract row named <c>PatchClass → Type.Method</c>),
/// and <see cref="Previous"/>/<see cref="Current"/> hold the two shapes as text
/// so a report never has to re-open a snapshot to say what moved.
/// </summary>
public sealed record ContractDifference(
	DifferenceKind Kind,
	DifferenceScope Scope,
	string Subject,
	string Previous,
	string Current,
	string Detail);
