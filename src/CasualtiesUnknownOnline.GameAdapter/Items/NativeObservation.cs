using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The single terminal observation produced by one native operation. The
/// coordinator guarantees one observation per operation; a remote-apply echo is
/// suppressed at Begin/Complete time so display/proxy operations never reach the
/// kernel as local facts.
/// </summary>
public sealed record NativeObservation(
	ulong OperationId,
	NativeOperationKind Kind,
	ulong Subject,
	string Before,
	IReadOnlyList<string> Fragments,
	string After)
{
	/// <summary>Stable diagnostic/checkpoint-friendly text form.</summary>
	public override string ToString() =>
		$"op={OperationId} kind={Kind} subject={Subject} before='{Before}' after='{After}' fragments=[{string.Join(",", Fragments)}]";
}
