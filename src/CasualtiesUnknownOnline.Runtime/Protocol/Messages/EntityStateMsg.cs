using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>One entity's full authoritative state: identity + position/look/velocity
/// + the packed pose flags (same bit layout as the old WriteEntity).</summary>
[ProtoContract]
public sealed class EntityStateMsg
{
	[ProtoMember(1)]
	public NetworkEntityIdMsg Id { get; set; } = new();

	[ProtoMember(2)]
	public NetVector2Msg Position { get; set; } = new();

	[ProtoMember(3)]
	public NetVector2Msg LookPos { get; set; } = new();

	[ProtoMember(4)]
	public NetVector2Msg Velocity { get; set; } = new();

	[ProtoMember(5)]
	public byte Flags { get; set; }

	// Extended pose bits. The 8 bit positions of Flags are FROZEN forever —
	// future pose/state details (attacking, dismembered, bleeding, ...) go here.
	// Assigned bits are never reused: 0x01 = IsAttacking (reserved, consumed
	// when attack-animation sync lands).
	[ProtoMember(6)]
	public uint ExtendedFlags { get; set; }

	/// <summary>
	/// Rolling per-swing sequence (wraps at 255): the render proxy replays the
	/// ArmsSwing clip when this CHANGES — every swing, even several inside one
	/// held IsAttacking window (rapid mining swings below the flag hold would
	/// otherwise merge into one rising edge). Additive: an old-version peer
	/// never sends it (stays 0) and the receiver falls back to the flag edge.
	/// </summary>
	[ProtoMember(7)]
	public byte SwingSeq { get; set; }

	/// <summary>
	/// The owner's LookTarget/CorpseScript override gaze target, or null when
	/// no override is active. Synced so a remote clone turns its head/eyes
	/// toward the same world point instead of only following the mouse aim.
	/// </summary>
	[ProtoMember(8)]
	public NetVector2Msg? LookOverridePos { get; set; }

	/// <summary>The owner's remaining override-look time (Body.overrideLookTime).</summary>
	[ProtoMember(9)]
	public float LookOverrideTime { get; set; }

	/// <summary>The owner's remaining scared-face time (Body.eyeScareTime).</summary>
	[ProtoMember(10)]
	public float EyeScareTime { get; set; }

	/// <summary>The owner's remaining panic-face time (Body.eyePanicTime).</summary>
	[ProtoMember(11)]
	public float EyePanicTime { get; set; }

	/// <summary>The owner's remaining eye-close time (Body.eyeCloseTime).</summary>
	[ProtoMember(12)]
	public float EyeCloseTime { get; set; }

	/// <summary>
	/// The owner's active workout/exercise type (Body.DoWorkout's
	/// WorkoutType): 0 = none, 1 = pushups, 2 = squats, 3 = plank. The clone
	/// replays the matching animator clips on change; no persistent fact is
	/// needed because the value is refreshed by the 20 Hz stream while the
	/// workout runs and returns to 0 when it ends.
	/// </summary>
	[ProtoMember(13)]
	public byte WorkoutType { get; set; }

	/// <summary>Domain → wire lives in <see cref="EntityStateMsgExtensions"/>;
	/// this applies the wire state back onto a live entity buffer (values + flags).</summary>
	public void ApplyTo(PlayerEntity target)
	{
		target.Position = Position.ToNetVector2();
		target.LookPos = LookPos.ToNetVector2();
		target.LookOverridePos = LookOverridePos?.ToNetVector2();
		target.LookOverrideTime = LookOverrideTime;
		target.EyeScareTime = EyeScareTime;
		target.EyePanicTime = EyePanicTime;
		target.EyeCloseTime = EyeCloseTime;
		target.Velocity = Velocity.ToNetVector2();
		target.IsRight = (Flags & 0x01) != 0;
		target.Standing = (Flags & 0x02) != 0;
		target.Alive = (Flags & 0x04) != 0;
		target.Conscious = (Flags & 0x08) != 0;
		target.Crouching = (Flags & 0x10) != 0;
		target.Sitting = (Flags & 0x20) != 0;
		target.Sleeping = (Flags & 0x40) != 0;
		target.Climbing = (Flags & 0x80) != 0;
		target.IsAttacking = (ExtendedFlags & 0x01u) != 0;
		target.SwingSeq = SwingSeq;
		target.WorkoutType = WorkoutType;
	}
}
