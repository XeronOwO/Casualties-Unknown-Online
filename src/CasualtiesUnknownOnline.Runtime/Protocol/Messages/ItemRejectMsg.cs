using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The host refused an item arbitration: the item was not in the authoritative
/// world-item table when the pickup was reported (it does not exist yet — the
/// spawn report is still in flight — or a faster writer already picked it up).
/// The guest rolls the local pickup back (item leaves the inventory back to
/// the world, at the position it picked it up from).
/// </summary>
[ProtoContract]
public sealed class ItemRejectMsg
{
	public enum Reason : byte
	{
		UnknownItem = 1,
	}

	[ProtoMember(1)]
	public ulong ItemId { get; set; }

	[ProtoMember(2)]
	public Reason Rejection { get; set; }
}
