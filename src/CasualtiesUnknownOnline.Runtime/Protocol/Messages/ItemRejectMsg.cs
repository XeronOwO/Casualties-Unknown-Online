using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The host refused an item arbitration:
/// UnknownItem — the item was not in the authoritative world-item table when
/// the pickup was reported (the spawn report is still in flight, or a faster
/// writer already picked it up). The guest rolls the local pickup back (item
/// leaves the inventory back to the world, at the position it picked it up from).
/// BlockAlreadyBroken — a reported block break's drops were refused (another
/// report of the same break won first-writer-wins). The breaker destroys its
/// local drops (they were never picked up — nothing to roll back to).
/// </summary>
[ProtoContract]
public sealed class ItemRejectMsg
{
	public enum Reason : byte
	{
		UnknownItem = 1,

		BlockAlreadyBroken = 2,
	}

	[ProtoMember(1)]
	public ulong ItemId { get; set; }

	[ProtoMember(2)]
	public Reason Rejection { get; set; }
}
