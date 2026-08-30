namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Pure host-side enemy combat policy constants. The values are the game's own
/// thresholds (and the host's multiplayer lunge tolerance), kept out of the
/// Game Adapter so the policy is lockable by Runtime tests and can be reused
/// when the combat director's decisions move into kernel processes.
/// </summary>
public static class EnemyCombatPolicy
{
	/// <summary>Spider bite range — FixedUpdate stops chasing at 1.5 units (SpiderHandler.cs:125), so contact happens inside that radius.</summary>
	public const float SpiderBiteRange = 1.5f;

	/// <summary>CrystalEnemy.close threshold (CrystalEnemy.cs:25) — the radius the game itself uses for player proximity.</summary>
	public const float CrystalCloseRange = 64f;

	/// <summary>Crystal Lunge raycasts 999 units (CrystalEnemy.cs:133) and ignores non-Body/non-Ground hits.</summary>
	public const float CrystalRayLength = 999f;

	/// <summary>Ray-vs-player tolerance (units) for the host's lunge arbitration — accept-first, not collision-box validation.</summary>
	public const float CrystalRayTolerance = 2f;
}
