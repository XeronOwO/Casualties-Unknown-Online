using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Runtime enemy-spawn binding (PURE — no Unity): the host allocates a NEW
/// enemy id for every animal that appears after the initial deterministic
/// mapping; the receiving side must pair those ids with the runtime copies it
/// created from the EntitySpawned channel, and materialize the missing ones
/// from the late-joiner snapshot's runtime-spawn facts. The judgments are
/// key/position-keyed and deterministic, so they can be L0-tested without
/// a game scene:
/// - live 20 Hz binding pairs by sorted position (the spawn message carries
///   the exact position, so both sides' lists are identical once every spawn
///   report arrived); a count mismatch or an out-of-tolerance pair waits —
///   never a greedy partial guess.
/// - snapshot matching binds a fact the host attributed to a creation record
///   to the local copy carrying that SAME creation key first
///   (<see cref="MatchRuntimeSpawnsByCreationKey"/> — distance-free), then
///   greedily binds the remaining keyless facts to the nearest same-prefab
///   local copy within tolerance; the unmatched facts are the materialization
///   list (the guest has no copy of that runtime enemy yet).
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
	/// Key-first snapshot matching (the identity pass): a runtime-spawn fact the
	/// host could attribute to a creation record carries that creation key, and
	/// a local copy materialized by an earlier snapshot / live report carries
	/// the same key on its <c>RuntimeEntityCreation</c> marker. Binding those by
	/// key is distance-free and prefab-name-free: the positional pass's 0.5-unit
	/// tolerance would otherwise materialize a SECOND copy of the same creation
	/// (with the same key) when the animal has already drifted. Keyed facts and
	/// keyed copies are consumed only here, so a keyed fact can never bind an
	/// unrelated markerless copy by position.
	/// </summary>
	internal static void MatchRuntimeSpawnsByCreationKey(
		IReadOnlyList<(int SpawnIndex, RuntimeEntityKey? CreationKey)> spawns,
		IReadOnlyList<(int CandidateIndex, RuntimeEntityKey? CreationKey)> candidates,
		out List<(int SpawnIndex, int CandidateIndex)> pairs,
		out List<int> unmatchedSpawnIndices)
	{
		pairs = [];
		unmatchedSpawnIndices = [];
		var usedCandidates = new HashSet<int>();

		foreach (var (spawnIndex, creationKey) in spawns)
		{
			if (creationKey is not { } key)
			{
				continue;
			}

			var bound = false;
			foreach (var (candidateIndex, candidateKey) in candidates)
			{
				if (usedCandidates.Contains(candidateIndex) || candidateKey != key)
				{
					continue;
				}

				pairs.Add((spawnIndex, candidateIndex));
				usedCandidates.Add(candidateIndex);
				bound = true;
				break;
			}

			if (!bound)
			{
				unmatchedSpawnIndices.Add(spawnIndex);
			}
		}
	}

	/// <summary>
	/// Snapshot matching orchestration: bind the facts the host attributed to a
	/// creation record by KEY first (distance-free), then bind the remaining
	/// keyless facts to the remaining keyless copies by prefab/position. A keyed
	/// fact is never positionally absorbed, and a keyed copy is never consumed by
	/// a keyless fact — the two identity classes stay separate.
	/// </summary>
	internal static void MatchRuntimeSpawnsByIdentity(
		IReadOnlyList<(int SpawnIndex, string PrefabId, NetVector2 Position, RuntimeEntityKey? CreationKey)> spawns,
		IReadOnlyList<(int CandidateIndex, string PrefabId, NetVector2 Position, RuntimeEntityKey? CreationKey)> candidates,
		out List<(int SpawnIndex, int CandidateIndex)> pairs,
		out List<int> unmatchedSpawnIndices)
	{
		var keyedSpawns = spawns
			.Select(s => (s.SpawnIndex, s.CreationKey))
			.ToList();
		var keyedCandidates = candidates
			.Select(c => (c.CandidateIndex, c.CreationKey))
			.ToList();
		MatchRuntimeSpawnsByCreationKey(keyedSpawns, keyedCandidates, out var keyedPairs, out var keyedUnmatched);

		var remainingSpawns = spawns
			.Where(s => s.CreationKey is null)
			.Select(s => (s.SpawnIndex, s.PrefabId, s.Position))
			.ToList();
		var usedCandidates = keyedPairs.Select(p => p.CandidateIndex).ToHashSet();
		var remainingCandidates = candidates
			.Where(c => c.CreationKey is null && !usedCandidates.Contains(c.CandidateIndex))
			.Select(c => (c.CandidateIndex, c.PrefabId, c.Position))
			.ToList();
		MatchRuntimeSpawns(remainingSpawns, remainingCandidates, out var positionalPairs, out var positionalUnmatched);

		// The positional pass reports positions inside the FILTERED candidate
		// list; the caller indexes its own candidate list, so map back to the
		// candidate's real index. (The keyed pass already returns real indices.)
		pairs = [.. keyedPairs];
		foreach (var (spawnIndex, filteredIndex) in positionalPairs)
		{
			pairs.Add((spawnIndex, remainingCandidates[filteredIndex].CandidateIndex));
		}

		unmatchedSpawnIndices = [.. keyedUnmatched, .. positionalUnmatched];
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
