using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A block was placed / air-written: guest → host as a report (the host
/// arbitrates — a placement must land on air, an air write on something —
/// then applies and relays), host → guest as the arbitrated answer: the
/// accepted relay reaches EVERY member including the reporter (its echo is the
/// acknowledgement), and a refused report gets the host's current cell as a
/// targeted correction. Block-space integer coordinates + the block id.
/// </summary>
[ProtoContract]
public sealed class BlockPlacedMsg
{
	[ProtoMember(1)]
	public int X { get; set; }

	[ProtoMember(2)]
	public int Y { get; set; }

	[ProtoMember(3)]
	public ushort Block { get; set; }
}
