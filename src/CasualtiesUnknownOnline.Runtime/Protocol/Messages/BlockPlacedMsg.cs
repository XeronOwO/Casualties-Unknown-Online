using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A block was placed (set to a non-air id): guest → host as a report (the
/// host arbitrates: the target must be air — then applies and relays), host →
/// guest as a broadcast relay (the source excluded — it already placed
/// locally). Block-space integer coordinates + the block id.
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
