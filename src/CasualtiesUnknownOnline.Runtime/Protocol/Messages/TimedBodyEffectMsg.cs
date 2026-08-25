using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A host-authoritative timed body effect that the target's local body must run
/// through the game's <c>CoUtils.DoTimedOp</c> 1 Hz tick semantics. The
/// effect is identified by its native operation id and carries the already
/// scaled duration; the target-side Game Adapter owns the exact native
/// per-tick lambda (including the game's per-action random rolls). The host
/// does not simulate these timed/random branches — the target re-reports
/// through the normal character snapshot path.
/// </summary>
[ProtoContract]
public sealed class TimedBodyEffectMsg
{
	/// <summary>The native <c>CoUtils.DoTimedOp</c> operation id (e.g. <c>highgradestimulant</c>).</summary>
	[ProtoMember(1)]
	public string EffectId { get; set; } = "";

	/// <summary>How many seconds the tick continues (same meaning as <c>CoUtils.DoTimedOp</c> duration).</summary>
	[ProtoMember(2)]
	public float DurationSeconds { get; set; }
}
