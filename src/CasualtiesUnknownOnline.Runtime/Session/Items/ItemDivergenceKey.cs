namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The value the guest's per-frame position-divergence line reports, as a value
/// type: the item and the BUCKET its distance falls in
/// (<see cref="ItemMotionState.DivergenceKeyDistance"/>). It is a value type on
/// purpose — <c>LogRepetitionGuard</c> compares values with <c>Equals</c>, and a
/// reference type would compare identity, which would make every frame a new
/// divergence and change nothing. The bucket is what keeps the comparison stable
/// while the gap is standing still, and what makes a gap that MOVES report again.
/// </summary>
internal readonly record struct ItemDivergenceKey(ulong ItemId, int DistanceBucket);
