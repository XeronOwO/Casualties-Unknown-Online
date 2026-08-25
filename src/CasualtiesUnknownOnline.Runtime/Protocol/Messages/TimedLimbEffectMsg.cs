using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A host-authoritative timed limb effect that the target's local body must run
/// through the game's <c>CoUtils.DoTimedOp</c> 1 Hz tick semantics. The target
/// applies the per-second delta for the given duration; the resulting body
/// state is then carried back by the normal character snapshot paths, so the
/// host never has to simulate the target's local timeline.
/// </summary>
[ProtoContract]
public sealed class TimedLimbEffectMsg
{
	/// <summary>The target limb the effect applies to.</summary>
	[ProtoMember(1)]
	public int LimbIndex { get; set; }

	/// <summary>How many seconds the tick continues (same meaning as <c>CoUtils.DoTimedOp</c> duration).</summary>
	[ProtoMember(2)]
	public float DurationSeconds { get; set; }

	/// <summary>Per-second bleed amount delta applied to the limb (negative reduces bleeding).</summary>
	[ProtoMember(3)]
	public float BleedPerSecond { get; set; }
}
