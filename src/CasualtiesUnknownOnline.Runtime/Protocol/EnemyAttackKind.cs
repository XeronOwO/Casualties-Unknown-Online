namespace CasualtiesUnknownOnline.Runtime.Protocol;

/// <summary>
/// Host-authoritative enemy attack kinds. The host owns the enemy simulation
/// (AI + physics), so an enemy that targets a remote player cannot rely on a
/// local collision — the host decides the attack and orders the victim's side
/// to apply it locally (local compute, remote verify/sync). Values start at 1
/// — protobuf omits zero, and Kind is never "unset".
/// </summary>
public enum EnemyAttackKind : byte
{
	/// <summary>SpiderHandler / SpiderHandlerTBE bite (SpiderHandler.DamageLimb).</summary>
	SpiderBite = 1,

	/// <summary>CrystalEnemy lunge (CrystalEnemy.Lunge, CrystalEnemy.cs:133-168).</summary>
	CrystalLunge = 2,
}
