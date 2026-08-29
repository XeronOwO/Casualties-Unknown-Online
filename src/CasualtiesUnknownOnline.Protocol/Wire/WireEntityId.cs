using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of the kernel entity identity (epoch/counter/generation).
/// </summary>
[ProtoContract]
public sealed class WireEntityId
{
	[ProtoMember(1)]
	public ulong Epoch { get; set; }

	[ProtoMember(2)]
	public uint Counter { get; set; }

	[ProtoMember(3)]
	public byte Generation { get; set; }
}
