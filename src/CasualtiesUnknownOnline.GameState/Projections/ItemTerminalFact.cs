using CasualtiesUnknownOnline.GameState.Domains.Items;

namespace CasualtiesUnknownOnline.GameState.Projections;

/// <summary>
/// The narrow semantic fact used by the diagnostic comparator: identity,
/// location family, owner, container, limb, anchor, and revision. The limb is
/// part of it because it is part of WHERE an applied item is: two applied items
/// on two limbs of one body agree on every other member of this fact.
/// </summary>
public readonly record struct ItemTerminalFact(
	ulong InstanceId,
	string DefinitionId,
	ItemLocationKind LocationKind,
	ulong Owner,
	ulong ParentItemId,
	int LimbIndex,
	float X,
	float Y,
	ulong Revision)
{
	public static ItemTerminalFact From(ItemState item) => new(
		item.Identity.InstanceId,
		item.Identity.DefinitionId,
		item.Location.Kind,
		item.Location.Owner.Value,
		item.Location.ParentItemId,
		item.Location.LimbIndex,
		item.Location.X,
		item.Location.Y,
		item.Revision);
}
