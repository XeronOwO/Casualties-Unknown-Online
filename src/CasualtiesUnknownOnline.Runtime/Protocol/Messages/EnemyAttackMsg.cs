using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → every in-world guest: the host-authoritative enemy simulation
/// performed an attack (a spider bite or a crystal lunge).
///
/// This is an ANNOUNCEMENT, not a verdict. The host owns the enemy's action and
/// its timing; whether that action connected with a player's body is judged by
/// THAT player's own client, against its own view of the frozen enemy copy and
/// its own body (the 2026-09-18 ruling: a host-side verdict punishes a victim
/// whose own screen showed a dodge). Remote render clones deliberately have no
/// colliders, so the host can never damage a guest through the game's collision
/// callback; the announcement is broadcast to every in-world guest, each one
/// judges its own connection, applies the game's own damage locally and reports
/// the terminal state through the attack-specific kernel event (EnemyBite for a
/// bite, EnemyLunge for a crystal lunge). Reliable — the announcement is
/// one-shot.
/// </summary>
[ProtoContract]
public sealed class EnemyAttackMsg
{
	/// <summary>The attacking enemy's NetworkEntityId (host-allocated; the guest resolves its frozen copy).</summary>
	[ProtoMember(1)]
	public NetworkEntityIdMsg EnemyId { get; set; } = new();

	/// <summary>The attack the host's enemy simulation performed.</summary>
	[ProtoMember(2)]
	public EnemyAttackKind Kind { get; set; }

	/// <summary>
	/// The host's per-enemy attack identity: a monotonic counter starting at 1
	/// for each enemy id, stamped by the host's enemy-sync service. The judging
	/// client applies at most one attack per identity (the victim-side ledger
	/// keeps the highest applied value per enemy), so a repeated or reordered
	/// announcement can never double-apply. The enemy id carries the world/layer
	/// epoch, so a counter from an older generation cannot collide with a later
	/// one, and 0 — a value the host never stamps — fails closed.
	/// </summary>
	[ProtoMember(3, IsRequired = true)]
	public uint AttackSeq { get; set; }
}
