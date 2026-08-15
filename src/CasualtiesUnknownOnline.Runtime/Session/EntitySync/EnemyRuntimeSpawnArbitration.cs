using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Runtime enemy-spawn binding (PURE — no Unity): the host allocates a NEW
/// enemy id for every animal that appears after the initial deterministic
/// mapping; the receiving side must pair those ids with the runtime copies it
/// created from the EntitySpawned channel, and materialize the missing ones
/// from the late-joiner snapshot's runtime-spawn facts. Both judgments are
/// position/prefab-keyed and deterministic, so they can be L0-tested without
/// a game scene:
/// - live 20 Hz binding pairs by sorted position (the spawn message carries
///   the exact position, so both sides' lists are identical once every spawn
///   report arrived); a count mismatch or an out-of-tolerance pair waits —
///   never a greedy partial guess.
/// - snapshot matching greedily binds each runtime-spawn fact to the nearest
///   same-prefab local copy within tolerance; the unmatched facts are the
///   materialization list (the guest has no copy of that runtime enemy yet).
/// </summary>
internal sealed class EnemyRuntimeSpawnArbitration
{
	/// <summary>
	/// Pair unbound host runtime states with unbound local runtime copies by
	/// their positions (x, y ascending), all-or-nothing: an unequal count or
	/// any pair farther than <see cref="EnemySpawnArbitration.PairTolerance"/>
	/// fails. Both lists hold the same spawn-command positions, so sorting is
	/// the deterministic identity; Unity's FindObjectsOfType order is not.
	/// </summary>
	internal static bool TryPairByPosition(
		IReadOnlyList<NetVector2> statePositions,
		IReadOnlyList<NetVector2> candidatePositions,
		out List<(int StateIndex, int CandidateIndex)> pairs)
	{
		pairs = [];
		if (statePositions.Count != candidatePositions.Count || statePositions.Count == 0)
		{
			return false;
		}

		var comparer = Comparer<NetVector2>.Create(EnemySpawnArbitration.Compare);
		var orderedStates = statePositions
			.Select((position, index) => (Index: index, Position: position))
			.OrderBy(e => e.Position, comparer)
			.ToList();
		var orderedCandidates = candidatePositions
			.Select((position, index) => (Index: index, Position: position))
			.OrderBy(e => e.Position, comparer)
			.ToList();

		for (var i = 0; i < orderedStates.Count; i++)
		{
			if (EnemySpawnArbitration.Distance(orderedStates[i].Position, orderedCandidates[i].Position)
				> EnemySpawnArbitration.PairTolerance)
			{
				pairs.Clear();
				return false;
			}

			pairs.Add((orderedStates[i].Index, orderedCandidates[i].Index));
		}

		return true;
	}

	/// <summary>
	/// Match runtime-spawn facts to local copies (same prefab, nearest within
	/// tolerance, deterministic tie-break), each side used at most once. The
	/// returned unmatched spawn indices are what the guest must materialize
	/// with <c>Utils.Create</c>.
	/// </summary>
	internal static void MatchRuntimeSpawns(
		IReadOnlyList<(int SpawnIndex, string PrefabId, NetVector2 Position)> spawns,
		IReadOnlyList<(int CandidateIndex, string PrefabId, NetVector2 Position)> candidates,
		out List<(int SpawnIndex, int CandidateIndex)> pairs,
		out List<int> unmatchedSpawnIndices)
	{
		pairs = [];
		unmatchedSpawnIndices = [];
		var usedCandidates = new bool[candidates.Count];

		// Deterministic processing order: same-prefab groups, then position.
		var orderedSpawns = spawns
			.OrderBy(s => s.PrefabId, StringComparer.Ordinal)
			.ThenBy(s => s.Position.X)
			.ThenBy(s => s.Position.Y)
			.ThenBy(s => s.SpawnIndex)
			.ToList();

		foreach (var spawn in orderedSpawns)
		{
			var bestCandidate = -1;
			var bestDistance = float.MaxValue;
			for (var i = 0; i < candidates.Count; i++)
			{
				if (usedCandidates[i] || !string.Equals(candidates[i].PrefabId, spawn.PrefabId, StringComparison.Ordinal))
				{
					continue;
				}

				var distance = EnemySpawnArbitration.Distance(spawn.Position, candidates[i].Position);
				if (distance > EnemySpawnArbitration.PairTolerance || distance >= bestDistance)
				{
					continue;
				}

				bestDistance = distance;
				bestCandidate = i;
			}

			if (bestCandidate < 0)
			{
				unmatchedSpawnIndices.Add(spawn.SpawnIndex);
				continue;
			}

			pairs.Add((spawn.SpawnIndex, bestCandidate));
			usedCandidates[bestCandidate] = true;
		}
	}
}
