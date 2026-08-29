using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>Wire form of one one-shot trap consumption fact.</summary>
[ProtoContract]
public sealed class WireTrapConsumption
{
	[ProtoMember(1)]
	public WireEntityPosition Position { get; set; } = new();

	[ProtoMember(2)]
	public int Kind { get; set; }

	[ProtoMember(3)]
	public byte Extra { get; set; }

	[ProtoMember(4)]
	public long TriggeredAtMs { get; set; }
}
