using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>Reply to <see cref="PingMsg"/> — echoes the sender's tick.</summary>
[ProtoContract]
public sealed class PongMsg
{
	[ProtoMember(1)]
	public long Ticks { get; set; }
}
