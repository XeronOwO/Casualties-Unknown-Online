using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// An item moved between slots (SwapSlots / SwitchHands): guest → host as a
/// report. Light by design — instance id + the new slot only; the host updates
/// the item's transfer-table entry and sends an ItemCorrection when its own
/// record disagrees (the slot is part of the authoritative item state).
/// </summary>
[ProtoContract]
public sealed class ItemSlotMsg
{
	[ProtoMember(1)]
	public ulong ItemId { get; set; }

	[ProtoMember(2)]
	public int SlotIndex { get; set; } // the item's new slot (Body.slots index)
}
