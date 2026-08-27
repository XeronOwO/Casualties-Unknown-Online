using CasualtiesUnknownOnline.GameState.Domains.Items;

namespace CasualtiesUnknownOnline.GameState.Projections;

/// <summary>
/// The narrow semantic fact used by the diagnostic comparator: identity,
/// location family, owner, container, anchor, and revision.
/// </summary>
public readonly record struct ItemTerminalFact(
	ulong InstanceId,
	string DefinitionId,
	ItemLocationKind LocationKind,
	ulong Owner,
	ulong ParentItemId,
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
		item.Location.X,
		item.Location.Y,
		item.Revision);
}
