using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The host refused an item arbitration:
/// UnknownItem — the item was not in the authoritative world-item table when
/// the pickup was reported (the spawn report is still in flight, or a faster
/// writer already picked it up). The guest rolls the local pickup back (item
/// leaves the inventory back to the world, at the position it picked it up from).
/// BlockAlreadyBroken — a reported block break's drops were refused: another
/// report of the same break won first-writer-wins, or (per drop) the destroyed
/// block's own table cannot produce that drop at all. The breaker destroys the
/// refused local drops (they were never picked up — nothing to roll back to),
/// and a per-drop refusal leaves the rest of the set registered and relayed.
/// TrapDropUnaccounted — a reported destructive entity event's drops were
/// refused: no drop table of the entities the host's own application of the
/// event destroyed or damaged can produce them. The reporter destroys the
/// refused local drops, exactly as for a refused break drop.
/// </summary>
[ProtoContract]
public sealed class ItemRejectMsg
{
	public enum Reason : byte
	{
		UnknownItem = 1,

		BlockAlreadyBroken = 2,

		TrapDropUnaccounted = 3,
	}

	[ProtoMember(1)]
	public ulong ItemId { get; set; }

	[ProtoMember(2)]
	public Reason Rejection { get; set; }
}
