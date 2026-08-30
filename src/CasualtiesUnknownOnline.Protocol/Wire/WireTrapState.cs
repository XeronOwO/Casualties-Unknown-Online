using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of one trap/mechanism state-machine fact.
/// </summary>
[ProtoContract]
public sealed class WireTrapState
{
	[ProtoMember(1)]
	public WireEntityPosition Position { get; set; } = new();

	[ProtoMember(2)]
	public int Kind { get; set; }

	[ProtoMember(3)]
	public int Phase { get; set; }

	[ProtoMember(4)]
	public byte Extra { get; set; }

	[ProtoMember(5)]
	public long TransitionedAtMs { get; set; }
}
