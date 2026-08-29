using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Integer world-cell position used to identify deterministic world entities on
/// the wire.
/// </summary>
[ProtoContract]
public sealed class WireEntityPosition
{
	[ProtoMember(1)]
	public int X { get; set; }

	[ProtoMember(2)]
	public int Y { get; set; }
}
