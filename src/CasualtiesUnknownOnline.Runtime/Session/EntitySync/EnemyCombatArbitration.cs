using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Pure host-side enemy combat arbitration. The game's enemy AI discovers
/// players through physics queries / PlayerCamera.main.body, which only see
/// the LOCAL body; the Game Adapter gathers the multiplayer candidate set
/// (host body + remote entity-stream positions) and this machine makes the
/// distance decisions — where the enemy aims and when its bite action fires.
/// Whether an attack CONNECTED is not decided here: the victim's own client
/// judges that (<see cref="EnemyAttackJudgment"/>, the 2026-09-18 ruling).
/// Keeping this pure makes the host-side decisions L0-testable without Unity.
/// </summary>
public static class EnemyCombatArbitration
{
	/// <summary>
	/// The nearest candidate within <paramref name="maxDistance"/>; null when
	/// none is in range. Ties keep the input order (the caller's candidate
	/// order is deterministic: local body first, then entity-stream order).
	/// </summary>
	public static EnemyTargetFact? SelectNearest(IEnumerable<EnemyTargetFact> candidates, NetVector2 origin, float maxDistance)
	{
		EnemyTargetFact? best = null;
		var bestDistance = maxDistance;
		foreach (var candidate in candidates)
		{
			var distance = Distance(origin, candidate.Position);
			if (distance < bestDistance)
			{
				best = candidate;
				bestDistance = distance;
			}
		}

		return best;
	}

	/// <summary>
	/// The spider's bite action: null while the game's cooldown/stun gates are
	/// closed or when no player is inside the bite radius, otherwise the nearest
	/// player (local or remote). The hit determination is no longer made here —
	/// the host announces the attack for the action this reports and every client
	/// judges its own connection (<see cref="EnemyAttackJudgment"/>).
	/// </summary>
	public static EnemyTargetFact? SelectBiteVictim(IEnumerable<EnemyTargetFact> candidates, NetVector2 origin,
		float biteRange, float biteCooldown, float stunTime)
	{
		if (biteCooldown > 0f || stunTime > 0f)
		{
			return null;
		}

		return SelectNearest(candidates, origin, biteRange);
	}

	private static float Distance(NetVector2 a, NetVector2 b)
	{
		var dx = a.X - b.X;
		var dy = a.Y - b.Y;
		return (float)Math.Sqrt(dx * dx + dy * dy);
	}
}
