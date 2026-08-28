using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of the kernel's single authoritative item location.
/// </summary>
[ProtoContract]
public sealed class WireItemLocation
{
	[ProtoMember(1)]
	public WireItemLocationKind Kind { get; set; }

	[ProtoMember(2)]
	public ulong Owner { get; set; }

	[ProtoMember(3)]
	public ulong ParentItemId { get; set; }

	[ProtoMember(4)]
	public float X { get; set; }

	[ProtoMember(5)]
	public float Y { get; set; }
}
