namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The native operation family. Each kind maps to one authoritative kernel
/// observation; the coordinator prevents a native operation from emitting more
/// than one observation or echoing a remote apply.
/// </summary>
public enum NativeOperationKind
{
	ItemDrop,
	ItemPickup,
	ItemThrow,
	ItemUse,
	ItemSlot,
	ItemDestroy,
	ItemCook,
	Craft
}
