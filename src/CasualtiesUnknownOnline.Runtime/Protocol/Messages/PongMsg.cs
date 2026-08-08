using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>Reply to <see cref="PingMsg"/> — echoes the sender's tick.</summary>
[ProtoContract]
public sealed class PongMsg
{
	[ProtoMember(1)]
	public long Ticks { get; set; }

	/// <summary>Static factory — same pattern as SceneStateMsg.From: the message
	/// assembles itself from domain data, no service-level assembly.</summary>
	public static PongMsg From(long ticks) => new() { Ticks = ticks };
}
