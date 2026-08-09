using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// An item left the world into a player's inventory: guest → host as a report
/// (the host arbitrates — first-writer-wins: the item must still be in the
/// world-item table; a rejected pickup gets an ItemReject back and the guest
/// rolls its local pickup back), host → guest as a broadcast of the winner
/// (the other guests remove the item from their world).
/// The optional Item is the picker's evidence (digest form: full top-level
/// state, contents as instance-id-only) — the host compares it against its own
/// world-table entry and sends an ItemCorrection when it differs (accept, never
/// reject — the host executes the transfer from its own entry either way).
/// </summary>
[ProtoContract]
public sealed class ItemPickupMsg
{
	[ProtoMember(1)]
	public ulong ItemId { get; set; }

	[ProtoMember(2)]
	public CharacterItemMsg? Item { get; set; } // digest evidence, absent = legacy (no arbitration)
}
