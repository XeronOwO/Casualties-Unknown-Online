using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// An item was used (Body.UseItem — drinking, eating, aiming a tool, …):
/// guest → host as a report. Using does not change the item's owner (unlike
/// pickup/drop), so the host only validates the reported state against the
/// item's entry (world table or the guest's transfer-table entry) and sends an
/// ItemCorrection when it differs — the usage itself is never rejected.
/// </summary>
[ProtoContract]
public sealed class ItemUseMsg
{
	[ProtoMember(1)]
	public ulong ItemId { get; set; }

	[ProtoMember(2)]
	public CharacterItemMsg Item { get; set; } = new(); // digest evidence (full top-level state, contents as instance-id-only)
}
