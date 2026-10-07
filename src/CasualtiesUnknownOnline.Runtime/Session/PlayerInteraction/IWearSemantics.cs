namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The game's own content fact behind the cross-player wearable (wear) chain, the
/// way <see cref="ILimbUseSemantics"/> backs the two limb-use chains: where a
/// wearable item goes when the game puts it on a body.
/// <para>
/// Both halves of that answer are data the item carries, never a CUO table:
/// <c>ItemInfo.wearable</c> is the flag <c>Body.WearWearable</c> gates on,
/// <c>ItemInfo.wearSlotId</c> is what the same flow's <c>Body.GetWearableBySlotID</c>
/// compares to decide the slot is occupied, and <c>ItemInfo.desiredWearLimb</c> is
/// the limb name <c>Body.LimbByName</c> resolves. The Runtime cannot reference the
/// game assembly, so the Game Adapter answers through this seam — and answers the
/// SAME instance the operator's own gestures ask, so the host and the operator read
/// one predicate rather than two.
/// </para>
/// <para>
/// The limb INDEX is part of the answer because the character snapshot encodes a
/// worn item as the negative slot <c>-(limbIndex + 2)</c>, so the index is the wire
/// shape the affected side restores from.
/// </para>
/// </summary>
public interface IWearSemantics
{
	/// <summary>
	/// The placement of a wearable item when the game wears it, or false when the
	/// item's own data does not make it one (or the wear limb cannot be resolved).
	/// The wear slot is the id the game's slot-occupancy check compares, so it
	/// identifies a collision the way the native flow does rather than by limb.
	/// </summary>
	bool TryGetWearPlacement(string itemId, out int limbIndex, out string wearSlotId);
}
