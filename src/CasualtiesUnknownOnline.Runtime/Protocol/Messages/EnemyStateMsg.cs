using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One enemy's authoritative presentation snapshot (host → guest). Only the
/// presentation subset is synced — position / velocity / rotation / health +
/// the packed presentation flags — not the AI internal state (target / stun
/// timers); attack side-effects (bites, drops) ride the existing damage/event
/// paths. Rotation is the Rigidbody2D z euler angle (degrees).
/// </summary>
[ProtoContract]
public sealed class EnemyStateMsg
{
	/// <summary>Presentation flag: the enemy presents a stunned/stuck pose (SpiderHandler.stunTime &gt; 0, CrystalEnemy.stuck). Bits are FROZEN forever — new flags append only (same discipline as EntityStateMsg.ExtendedFlags).</summary>
	public const uint FlagStunned = 0x01;

	[ProtoMember(1)]
	public NetworkEntityIdMsg Id { get; set; } = new();

	[ProtoMember(2)]
	public NetVector2Msg Position { get; set; } = new();

	[ProtoMember(3)]
	public NetVector2Msg Velocity { get; set; } = new();

	[ProtoMember(4)]
	public float Rotation { get; set; }

	[ProtoMember(5)]
	public float Health { get; set; }

	/// <summary>Packed presentation flags (see <see cref="FlagStunned"/>).</summary>
	[ProtoMember(6)]
	public uint PresentationFlags { get; set; }

	/// <summary>
	/// Host-captured spider leg IK targets (IKHandle.targetPos, world space).
	/// Null/empty for non-spider enemies; the positional part of the legs is
	/// already carried by the entity transform.
	/// </summary>
	[ProtoMember(7)]
	public List<NetVector2Msg>? SpiderLegTargets { get; set; }

	/// <summary>
	/// Host-captured CrystalEnemy wind-up progress in seconds (0 = no telegraph;
	/// > 0 = the pre-lunge line is visible). Only meaningful for crystal
	/// enemies; the receiver reproduces the line fade from this absolute value.
	/// </summary>
	[ProtoMember(8)]
	public float CrystalWindupAmount { get; set; }

	/// <summary>
	/// Host-captured end point of the CrystalEnemy telegraph line (world space).
	/// Null when no line is active; the start point is the entity transform,
	/// which is already position-synced.
	/// </summary>
	[ProtoMember(9)]
	public NetVector2Msg? CrystalLineEnd { get; set; }

	/// <summary>Wire → domain; the reverse lives in <see cref="EnemyEntity.ToEnemyStateMsg"/>.</summary>
	public void ApplyTo(EnemyEntity target)
	{
		target.EntityId = Id.ToNetworkEntityId();
		target.Position = Position.ToNetVector2();
		target.Velocity = Velocity.ToNetVector2();
		target.Rotation = Rotation;
		target.Health = Health;
		target.Stunned = (PresentationFlags & FlagStunned) != 0;
		target.SpiderLegTargets = SpiderLegTargets?.ConvertAll(v => v.ToNetVector2());
		target.CrystalWindupAmount = CrystalWindupAmount;
		target.CrystalLineEnd = CrystalLineEnd?.ToNetVector2();
	}
}
