namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// The authoritative kernel fact for one item: identity, per-item revision,
/// exactly one location, and the kernel-owned persistent item payload. The
/// payload is separate from location so every item fact has a single owner; a
/// container subtree is represented by separate ItemState entries with
/// Contained locations, never by nested copies inside this record.
/// </summary>
public readonly record struct ItemState(ItemIdentity Identity, ulong Revision, ItemLocation Location)
{
	/// <summary>
	/// The save-shaped item payload. The positional constructor is preserved for
	/// Phase A call sites and tests that only need identity/location; the kernel
	/// fills this through Commands that carry item data.
	/// </summary>
	public ItemData Data { get; init; } = ItemData.Empty;

	public ItemState With(ulong revision, ItemLocation location) => this with { Revision = revision, Location = location };

	public ItemState With(ulong revision, ItemLocation location, ItemData data) => this with
	{
		Revision = revision,
		Location = location,
		Data = data
	};
}
