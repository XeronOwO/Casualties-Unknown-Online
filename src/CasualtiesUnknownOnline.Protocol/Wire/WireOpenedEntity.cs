using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>Wire form of one opened lockable-entity fact.</summary>
[ProtoContract]
public sealed class WireOpenedEntity
{
	[ProtoMember(1)]
	public WireEntityPosition Position { get; set; } = new();
}
