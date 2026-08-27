namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// One item's current location. A world location carries optional world
/// coordinates and an optional parent container; carried/contained locations
/// carry the owning actor and container parent. Terminal is consumed/destroyed
/// and cannot be resurrected.
/// </summary>
public readonly record struct ItemLocation(
	ItemLocationKind Kind,
	ActorId Owner,
	ulong ParentItemId,
	float X,
	float Y)
{
	public static ItemLocation World(float x, float y, ulong parentItemId = 0) =>
		new(ItemLocationKind.World, default, parentItemId, x, y);

	public static ItemLocation Carried(ActorId owner) =>
		new(ItemLocationKind.Carried, owner, 0, 0, 0);

	public static ItemLocation Contained(ActorId owner, ulong parentItemId) =>
		new(ItemLocationKind.Contained, owner, parentItemId, 0, 0);

	public static ItemLocation Terminal() =>
		new(ItemLocationKind.Terminal, default, 0, 0, 0);
}
