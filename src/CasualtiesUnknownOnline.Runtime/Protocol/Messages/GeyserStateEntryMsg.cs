using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>One geyser's liquid type: the geyser's world position (deterministic entities — both sides have the same object at the same place) and the host-side value (1/2 — never 0, so the protobuf zero-omission cannot bite).</summary>
[ProtoContract]
public sealed class GeyserStateEntryMsg
{
	[ProtoMember(1)]
	public NetVector2Msg Position { get; set; } = new();

	[ProtoMember(2)]
	public byte LiquidType { get; set; }
}
