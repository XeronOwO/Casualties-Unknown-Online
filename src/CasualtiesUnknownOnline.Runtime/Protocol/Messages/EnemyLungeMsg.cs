using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A crystal lunge hit a player (CrystalEnemy.Lunge, CrystalEnemy.cs:133-168):
/// the victim's local body already applied the damage (local compute — either
/// the host's own crystal hit the host locally, or a host-ordered EnemyAttack
/// applied on the victim's side). This DTO is the presentation projection of the
/// kernel <c>EnemyLungeResultEvent</c> — the hit limb plus the body's
/// adrenaline/stamina — so every peer applies the exact same state (exact
/// rebuild, never a delta). The source victim is not re-projected. The 1 Hz
/// character snapshot stays the fallback for the other body fields
/// Ragdoll/Scream change.
/// </summary>
[ProtoContract]
public sealed class EnemyLungeMsg
{
	/// <summary>The hit player (the reporter's own SteamId for a guest report).</summary>
	[ProtoMember(1)]
	public ulong VictimSteamId { get; set; }

	/// <summary>The hit limb's post-lunge terminal state (Index = the limb in Body.limbs).</summary>
	[ProtoMember(2)]
	public CharacterLimbMsg Limb { get; set; } = new();

	/// <summary>Post-lunge body adrenaline (the lunge adds 70f).</summary>
	[ProtoMember(3)]
	public float Adrenaline { get; set; }

	/// <summary>Post-lunge body stamina (the lunge sets it to 100f).</summary>
	[ProtoMember(4)]
	public float Stamina { get; set; }
}
