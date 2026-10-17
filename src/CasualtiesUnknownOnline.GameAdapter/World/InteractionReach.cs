namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// How far a player's own hand is behind a world write. One number, shared by every bound that asks "could this
/// player have reached that cell" — a hand that can pick a lock is also the hand that can break the wall beside it,
/// so splitting the value would let two answers to the same question disagree.
/// <para>
/// Deliberately generous. The defects these bounds close were hundreds of cells away, while a legitimate melee or
/// tool reach is a few cells at most: the value stops the impossible without having to argue about the merely long,
/// and an out-of-reach write is still answered rather than silently dropped.
/// </para>
/// </summary>
internal static class InteractionReach
{
	/// <summary>The furthest a player's own gesture may affect a world cell, in cells.</summary>
	internal const float Cells = 8f;
}
