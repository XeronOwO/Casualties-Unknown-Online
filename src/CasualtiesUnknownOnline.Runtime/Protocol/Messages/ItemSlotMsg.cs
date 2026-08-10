using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// An item moved between slots (SwapSlots / SwitchHands): guest → host as a
/// report. The digest evidence rides along (the item's full top-level state at
/// the new slot) — the host records the slot in the transfer-table entry when
/// one exists, and broadcasts the report as the carried-fact event when it
/// does not (a starting-supply item never passed a pickup, so the host has no
/// entry — the guest is the fact source for its own body, same as a use).
/// </summary>
[ProtoContract]
public sealed class ItemSlotMsg
{
	[ProtoMember(1)]
	public ulong ItemId { get; set; }

	[ProtoMember(2)]
	public int SlotIndex { get; set; } // the item's new slot (Body.slots index)

	[ProtoMember(3)]
	public CharacterItemMsg Item { get; set; } = new(); // digest evidence (full top-level state at the new slot)
}
