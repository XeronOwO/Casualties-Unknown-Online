using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>One in-world player an enemy may target — engine-agnostic input for the host-side combat arbitration.</summary>
public readonly struct EnemyTargetFact(ulong steamId, NetVector2 position)
{
	public readonly ulong SteamId = steamId;

	public readonly NetVector2 Position = position;
}

/// <summary>
/// Pure host-side enemy combat arbitration. The game's enemy AI discovers
/// players through physics queries / PlayerCamera.main.body, which only see
/// the LOCAL body; the Game Adapter gathers the multiplayer candidate set
/// (host body + remote entity-stream positions) and this machine makes the
/// distance/ray decisions. Keeping it pure makes the host-authoritative
/// judgment L0-testable without Unity.
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
	/// The spider-bite victim: null while the game's cooldown/stun gates are
	/// closed or when no player is inside the bite radius. The nearest player
	/// (local or remote) is returned; the caller's order policy decides whether
	/// the local body rides the native collision path or a remote victim gets a
	/// host-ordered attack.
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

	/// <summary>
	/// The first player along a crystal lunge ray before the first ground hit:
	/// the candidate must be in front of the origin, within
	/// <paramref name="rayTolerance"/> of the ray, and closer along the ray
	/// than <paramref name="groundDistance"/> (999 = the game's own raycast
	/// length, CrystalEnemy.cs:133). Ties keep the input order.
	/// </summary>
	public static EnemyTargetFact? SelectLungeVictim(IEnumerable<EnemyTargetFact> candidates, NetVector2 origin,
		NetVector2 direction, float groundDistance, float rayTolerance)
	{
		EnemyTargetFact? best = null;
		var bestAlong = groundDistance;
		foreach (var candidate in candidates)
		{
			var toX = candidate.Position.X - origin.X;
			var toY = candidate.Position.Y - origin.Y;
			var alongRay = toX * direction.X + toY * direction.Y;
			if (alongRay <= 0f || alongRay >= bestAlong)
			{
				continue;
			}

			// 2D cross product magnitude = perpendicular distance when direction is normalized.
			var perpendicular = Math.Abs(toX * direction.Y - toY * direction.X);
			if (perpendicular > rayTolerance)
			{
				continue;
			}

			best = candidate;
			bestAlong = alongRay;
		}

		return best;
	}

	private static float Distance(NetVector2 a, NetVector2 b)
	{
		var dx = a.X - b.X;
		var dy = a.Y - b.Y;
		return (float)Math.Sqrt(dx * dx + dy * dy);
	}
}
