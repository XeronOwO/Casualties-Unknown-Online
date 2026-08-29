using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of one persistent fluid-region checkpoint fact.
/// </summary>
[ProtoContract]
public sealed class WireFluidRegionState
{
	[ProtoMember(1)]
	public int ChunkX { get; set; }

	[ProtoMember(2)]
	public int ChunkY { get; set; }

	[ProtoMember(3)]
	public int TotalAmount { get; set; }

	[ProtoMember(4)]
	public byte MainType { get; set; }

	[ProtoMember(5)]
	public long UpdatedAtMs { get; set; }
}
