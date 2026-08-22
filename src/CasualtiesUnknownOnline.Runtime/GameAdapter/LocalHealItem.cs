namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// One local carried heal-profile item, projected for the Online UI's explicit
/// item selector. The instance id is the wire key the host already accepts in
/// <c>PlayerHealRequestMsg.ItemInstanceId</c>; the item id is display text only.
/// </summary>
public sealed record LocalHealItem(
	ulong InstanceId,
	string ItemId);
