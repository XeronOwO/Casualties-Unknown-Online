using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The runtime entity-creation match judgment (PURE — no Unity): which local
/// BuildingEntity a creation record binds to. Extracted from the adapter's
/// <c>EntitySpawnSync.FindExisting</c> so the dedup contract is unit-testable
/// without a game scene, exactly like <c>TrapLayoutAlign</c> and
/// <c>EnemyRuntimeSpawnArbitration</c>.
/// <para>
/// The radius is <see cref="MatchRadius"/> = 1, not 3: a 3 m radius absorbed
/// consecutive spawns of the same prefab (the observed bug — three spawned
/// turrets ~1-2 m apart, only the first reached the peer). The match is the
/// first candidate in enumeration order, which keeps the apply idempotent: a
/// repeated creation record binds to the same copy instead of creating a
/// second one. A per-player tutorial prop is never a bind target — binding a
/// shared domain entity to it would let one player's creation absorb another
/// player's private course object.
/// </para>
/// </summary>
internal static class RuntimeEntityMatch
{
	/// <summary>The same-prefab match radius (metres) — see the class remarks.</summary>
	internal const float MatchRadius = 1f;

	/// <summary>One local candidate the adapter scanned (the tutorial flag and the creation-key match are resolved by the adapter).</summary>
	internal readonly record struct Candidate(string Id, float X, float Y, bool IsTutorialProp, bool IsSameCreation);

	/// <summary>
	/// The index of the local copy a creation record binds to: first a candidate
	/// carrying the SAME creation marker (prefab id + creation cell) — that is
	/// the record's identity even after the entity drifted across cells (a
	/// BuildingEntity's Rigidbody2D becomes Dynamic while its chunk is visible,
	/// BuildingEntity.cs:54) — then the first same-prefab, non-tutorial
	/// candidate strictly inside <see cref="MatchRadius"/> of the recorded
	/// position (the generated-world / legacy fallback). -1 when none binds.
	/// </summary>
	internal static int FindIndex(IReadOnlyList<Candidate> candidates, string id, float x, float y)
	{
		for (var i = 0; i < candidates.Count; i++)
		{
			if (candidates[i].IsSameCreation)
			{
				return i;
			}
		}

		var radiusSquared = MatchRadius * MatchRadius;
		for (var i = 0; i < candidates.Count; i++)
		{
			var candidate = candidates[i];
			if (candidate.IsTutorialProp || !string.Equals(candidate.Id, id, StringComparison.Ordinal))
			{
				continue;
			}

			var dx = candidate.X - x;
			var dy = candidate.Y - y;
			if ((dx * dx) + (dy * dy) < radiusSquared)
			{
				return i;
			}
		}

		return -1;
	}
}
