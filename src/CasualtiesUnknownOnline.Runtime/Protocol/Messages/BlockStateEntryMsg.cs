using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>One changed block: integer block-space coordinates + the current block id.</summary>
[ProtoContract]
public sealed class BlockStateEntryMsg
{
	[ProtoMember(1)]
	public int X { get; set; }

	[ProtoMember(2)]
	public int Y { get; set; }

	[ProtoMember(3)]
	public ushort Block { get; set; }
}
