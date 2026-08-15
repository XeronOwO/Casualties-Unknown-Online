using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: the host-authoritative enemy simulation decided an attack on
/// a remote player. Remote render clones deliberately have no colliders
/// (RemoteBodyFactory disables them — they must never participate in physics),
/// so the host cannot damage the guest through the game's collision callback.
/// This message carries the decision; the victim's side applies the attack
/// locally (the game's own damage methods on its own body) and reports the
/// post-attack state back through the attack-specific event (EnemyBite for a
/// bite, EnemyLunge for a crystal lunge). Reliable — the command is one-shot.
/// </summary>
[ProtoContract]
public sealed class EnemyAttackMsg
{
	/// <summary>The attacking enemy's NetworkEntityId (host-allocated; the guest resolves its frozen copy).</summary>
	[ProtoMember(1)]
	public NetworkEntityIdMsg EnemyId { get; set; } = new();

	/// <summary>The attacked player (the guest the command is sent to).</summary>
	[ProtoMember(2)]
	public ulong VictimSteamId { get; set; }

	/// <summary>The attack to apply locally.</summary>
	[ProtoMember(3)]
	public EnemyAttackKind Kind { get; set; }

	/// <summary>
	/// The limb the host selected from the victim's render clone (-1 = the
	/// victim picks its closest non-dismembered limb, the game's own
	/// Body.GetClosestLimb semantics — Body.cs:1826). Protobuf omits zero, so
	/// -1 travels explicitly; limb 0 is a valid limb.
	/// </summary>
	[ProtoMember(4, IsRequired = true)]
	public int LimbIndex { get; set; } = -1;
}
