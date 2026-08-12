using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>Diagnostics round-trip probe (RTT measurement).</summary>
[ProtoContract]
public sealed class PingMsg
{
	[ProtoMember(1)]
	public long Ticks { get; set; }

	/// <summary>Factory for a ping stamped at the given moment (the caller's
	/// ITimeSource.UtcNowTicks) — the RTT is measured against the echo's tick.</summary>
	public static PingMsg At(long utcTicks) => new() { Ticks = utcTicks };
}
