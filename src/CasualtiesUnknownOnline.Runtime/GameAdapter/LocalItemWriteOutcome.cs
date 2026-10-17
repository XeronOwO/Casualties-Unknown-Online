using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// One <see cref="ILocalItemAccess.WriteLocalItem"/> answer. <see cref="Written"/> is the
/// state the adapter RE-READ after applying the document — the report carries it, never
/// the caller's own tree, so a hook that swallowed a write cannot report it.
/// </summary>
/// <param name="Result">What happened.</param>
/// <param name="Refusal">The adapter's own sentence when <see cref="Result"/> is <see cref="LocalItemWriteResult.Refused"/>.</param>
/// <param name="Written">The re-read capture when the write applied; null otherwise.</param>
/// <param name="Note">
/// One consequence of the written state the operator should read, in the adapter's own
/// knowledge (a condition that reached 0 is destroyable by the game's rule). Null when the
/// state carries none; the command appends it to the answer verbatim.
/// </param>
public readonly record struct LocalItemWriteOutcome(
	LocalItemWriteResult Result,
	string? Refusal,
	CharacterItemMsg? Written,
	string? Note = null)
{
	/// <summary>Whether the local object now carries the state.</summary>
	public bool Applied => Result == LocalItemWriteResult.Applied;
}
