namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// The authoritative kernel fact for one item: identity, per-item revision, and
/// exactly one location.
/// </summary>
public readonly record struct ItemState(ItemIdentity Identity, ulong Revision, ItemLocation Location)
{
	public ItemState With(ulong revision, ItemLocation location) => this with { Revision = revision, Location = location };
}
