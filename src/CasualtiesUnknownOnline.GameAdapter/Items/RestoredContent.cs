namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// What restoring one container content produced: the object that now exists, whether the container took it, and
/// the container's own reason when it did not.
///
/// <para>
/// The object is handed back on a refusal on purpose. A restore has to materialize the item BEFORE the
/// container's rule can be asked — the rule reads the item's own weight and tags — so a refusal leaves a real
/// object in hand, and destroying it is not a local act: <c>Item.OnDestroy</c> reports the instance id as dead
/// at the end of the frame, the host's kernel stores it TERMINAL, and any object a caller then builds with that
/// same id is swept away. That is how the handed-over toolbox disappeared from both players in batch
/// <c>20261021-a</c> (<c>give-item-to-another-player</c> row 4). A caller therefore PLACES an object it receives
/// with <see cref="Loaded"/> false; the only caller that discards one is the bulk restore, whose policy is named
/// and recorded (<see cref="ItemStateCodec.RestoreContentDiscarding"/>).
/// </para>
/// </summary>
internal readonly record struct RestoredContent(Item? Item, bool Loaded, string? Refusal)
{
	/// <summary>Nothing was created at all: the definition's prefab does not load here, or it carries no <c>Item</c>. There is no object to place.</summary>
	internal static RestoredContent NotCreated() => new(null, false, null);

	/// <summary>The item is in the container.</summary>
	internal static RestoredContent Landed(Item item) => new(item, true, null);

	/// <summary>The item was materialized and the container refused it; the caller owns <paramref name="item"/> now.</summary>
	internal static RestoredContent Refused(Item item, string why) => new(item, false, why);
}
