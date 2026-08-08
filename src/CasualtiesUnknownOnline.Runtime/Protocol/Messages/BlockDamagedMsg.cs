using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>Either side → peer: a block was damaged at a world position (local compute, remote verify/sync).</summary>
[ProtoContract]
public sealed class BlockDamagedMsg
{
	[ProtoMember(1)]
	public NetVector2Msg Position { get; set; } = new();

	[ProtoMember(2)]
	public float Damage { get; set; }
}
