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

	/// <summary>Wire → domain; the reverse lives in <see cref="EnemyEntity.ToEnemyStateMsg"/>.</summary>
	public void ApplyTo(EnemyEntity target)
	{
		target.EntityId = Id.ToNetworkEntityId();
		target.Position = Position.ToNetVector2();
		target.Velocity = Velocity.ToNetVector2();
		target.Rotation = Rotation;
		target.Health = Health;
		target.Stunned = (PresentationFlags & FlagStunned) != 0;
	}
}
