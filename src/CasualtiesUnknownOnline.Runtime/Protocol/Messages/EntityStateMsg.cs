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

	/// <summary>Domain → wire lives in <see cref="EntityStateMsgExtensions"/>;
	/// this applies the wire state back onto a live entity buffer (values + flags).</summary>
	public void ApplyTo(PlayerEntity target)
	{
		target.Position = Position.ToNetVector2();
		target.LookPos = LookPos.ToNetVector2();
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
	}
}
