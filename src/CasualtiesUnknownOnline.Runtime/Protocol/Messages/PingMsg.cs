using System;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>Diagnostics round-trip probe (RTT measurement).</summary>
[ProtoContract]
public sealed class PingMsg
{
	[ProtoMember(1)]
	public long Ticks { get; set; }

	/// <summary>Factory for a ping stamped with the current moment (DateTime.Now
	/// style) — the RTT is measured against the echo's tick.</summary>
	public static PingMsg Now => new() { Ticks = DateTime.UtcNow.Ticks };
}
