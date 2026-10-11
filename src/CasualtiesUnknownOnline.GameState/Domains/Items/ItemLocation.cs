namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// One item's current location. A world location carries optional world
/// coordinates and an optional parent container; carried/contained locations
/// carry the owning actor and container parent; an applied location carries the
/// owner of the limb it is installed on and that limb's index; terminal is
/// consumed/destroyed and cannot be resurrected. A field a family does not use
/// carries its neutral value, so the record stays one shape for every family and
/// a location is compared as a whole.
/// </summary>
public readonly record struct ItemLocation(
	ItemLocationKind Kind,
	ActorId Owner,
	ulong ParentItemId,
	int LimbIndex,
	float X,
	float Y)
{
	/// <summary>The limb index of a location that names no limb — every family but Applied.</summary>
	public const int NoLimb = -1;

	public static ItemLocation World(float x, float y, ulong parentItemId = 0) =>
		new(ItemLocationKind.World, default, parentItemId, NoLimb, x, y);

	public static ItemLocation Carried(ActorId owner) =>
		new(ItemLocationKind.Carried, owner, 0, NoLimb, 0, 0);

	public static ItemLocation Contained(ActorId owner, ulong parentItemId) =>
		new(ItemLocationKind.Contained, owner, parentItemId, NoLimb, 0, 0);

	/// <summary>
	/// The item IS the installed form of a limb component of
	/// <paramref name="owner"/>'s limb <paramref name="limbIndex"/> (an index into
	/// the body's limb array, the same index <c>CharacterLimbMsg.Index</c> uses).
	/// The component type is not stored: the item's own definition states it.
	/// </summary>
	public static ItemLocation Applied(ActorId owner, int limbIndex) =>
		new(ItemLocationKind.Applied, owner, 0, limbIndex, 0, 0);

	public static ItemLocation Terminal() =>
		new(ItemLocationKind.Terminal, default, 0, NoLimb, 0, 0);
}
