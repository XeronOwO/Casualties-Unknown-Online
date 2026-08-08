using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>Diagnostics round-trip probe (RTT measurement).</summary>
[ProtoContract]
public sealed class PingMsg
{
	[ProtoMember(1)]
	public long Ticks { get; set; }
}
