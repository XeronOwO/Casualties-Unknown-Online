using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>Wire form of one building-entity health fact.</summary>
[ProtoContract]
public sealed class WireBuildingEntityHealth
{
	[ProtoMember(1)]
	public WireEntityPosition Position { get; set; } = new();

	[ProtoMember(2)]
	public float Health { get; set; }
}
