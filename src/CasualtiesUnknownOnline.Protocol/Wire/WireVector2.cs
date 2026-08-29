using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of a two-dimensional continuous vector used by the state stream.
/// </summary>
[ProtoContract]
public sealed class WireVector2
{
	[ProtoMember(1)]
	public float X { get; set; }

	[ProtoMember(2)]
	public float Y { get; set; }
}
