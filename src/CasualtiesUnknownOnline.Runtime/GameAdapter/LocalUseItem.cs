namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// One local carried cross-player consumable item, projected for the Online
/// UI's explicit item selector. The instance id is the wire key the host
/// accepts in <c>PlayerItemUseRequestMsg.ItemInstanceId</c>; the item id is
/// display text only.
/// </summary>
public sealed record LocalUseItem(
	ulong InstanceId,
	string ItemId);
