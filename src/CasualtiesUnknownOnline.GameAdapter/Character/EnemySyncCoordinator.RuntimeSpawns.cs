using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Runtime enemy-spawn binding half of <see cref="EnemySyncCoordinator"/> (the
/// partial split at the 600-line gate): materializes or binds the runtime-created
/// animal copies from the late-joiner snapshot facts, and pairs the unbound
/// 20 Hz host ids with the EntitySpawned-created local copies. All state still
/// lives in the main partial declaration.
/// </summary>
internal sealed partial class EnemySyncCoordinator
{
	/// <summary>Bind every runtime-spawn fact to an existing same-prefab local
	/// copy when one is within tolerance; materialize the rest (the fresh
	/// late-joiner case). Matched copies are frozen before any further state.</summary>
	private void MaterializeRuntimeSpawns(IReadOnlyList<EnemySpawnEntryMsg> spawns)
	{
		if (spawns.Count == 0)
		{
			return;
		}

		var animals = FindAnimals();
		var candidates = new List<BuildingEntity>();
		var candidateFacts = new List<(int CandidateIndex, string PrefabId, NetVector2 Position)>();
		foreach (var entity in animals)
		{
			if (_runtimeAnimalCopies.Contains(entity) && !_idByEntity.ContainsKey(entity))
			{
				candidateFacts.Add((candidates.Count, entity.id, new NetVector2(entity.transform.position.x, entity.transform.position.y)));
				candidates.Add(entity);
			}
		}

		var spawnFacts = spawns
			.Select((spawn, index) => (SpawnIndex: index, spawn.PrefabId, Position: spawn.Position.ToNetVector2()))
			.ToList();
		EnemyRuntimeSpawnArbitration.MatchRuntimeSpawns(spawnFacts, candidateFacts, out var pairs, out var unmatchedSpawnIndices);

		foreach (var (spawnIndex, candidateIndex) in pairs)
		{
			var spawn = spawns[spawnIndex];
			Bind(candidates[candidateIndex], spawn.Id.ToNetworkEntityId(), runtimeSpawn: false);
			Freeze(candidates[candidateIndex]);
		}

		foreach (var spawnIndex in unmatchedSpawnIndices)
		{
			CreateRuntimeSpawn(spawns[spawnIndex]);
		}
	}

	private void CreateRuntimeSpawn(EnemySpawnEntryMsg spawn)
	{
		var id = spawn.Id.ToNetworkEntityId();
		if (_entityById.TryGetValue(id, out var alreadyBound) && alreadyBound != null) // Unity object — ==
		{
			return;
		}

		var pos = new Vector2(spawn.Position.X, spawn.Position.Y);
		var createdGo = Utils.Create(spawn.PrefabId, pos, 0f);
		if (createdGo == null) // Unity object — == (unknown prefab — the sender's prefab set differs)
		{
			_log.LogWarning("[Enemy] cannot materialize runtime spawn {Id} (prefab {Prefab}) — Resources.Load returned nothing.",
				id, spawn.PrefabId);
			return;
		}

		var created = createdGo.GetComponent<BuildingEntity>();
		if (created == null) // Unity object — ==
		{
			_log.LogWarning("[Enemy] materialized {Prefab} for runtime spawn {Id} has no BuildingEntity.", spawn.PrefabId, id);
			return;
		}

		created.transform.eulerAngles = new Vector3(0f, 0f, spawn.Rotation);
		createdGo.AddComponent<SpawnReplayMarker>(); // its own Start must not re-report the materialization
		Bind(created, id, runtimeSpawn: false);
		Freeze(created);
		_log.LogInformation("[Enemy] materialized runtime spawn {Id} (prefab {Prefab}) at ({X:F1},{Y:F1}).",
			id, spawn.PrefabId, spawn.Position.X, spawn.Position.Y);
	}

	private void ApplyAllStates()
	{
		foreach (var state in _enemies.Enemies)
		{
			if (_entityById.TryGetValue(state.EntityId, out var entity) && entity != null) // Unity object — ==
			{
				Apply(entity, state);
			}
		}
	}

	private static void Freeze(BuildingEntity entity)
	{
		if (entity.GetComponent<RemoteEnemyDriver>() == null) // Unity object — ==
		{
			entity.gameObject.AddComponent<RemoteEnemyDriver>();
		}

		var rb = entity.GetComponent<Rigidbody2D>();
		if (rb != null)
		{
			rb.bodyType = RigidbodyType2D.Static; // no physics simulation on the guest copy
		}
	}

	// ---- Guest: drive the frozen copies from the batch ----

	private void OnEnemyStateReceived()
	{
		TryBindRuntimeSpawns();
		ApplyAllStates();
	}

	/// <summary>
	/// A runtime enemy id the host allocated after the initial mapping has no
	/// local copy bound yet. The EntitySpawned channel created the local copy at
	/// the same position, so the unbound host states and the unbound local
	/// animals pair deterministically by position. All-or-nothing: while spawn
	/// reports are still arriving (16 ticks over 1.6 s), the counts differ and
	/// nothing pairs — the next 20 Hz batch retries after every report landed.
	/// </summary>
	private void TryBindRuntimeSpawns()
	{
		if (!_mappingEstablished)
		{
			return; // the generation baseline is not safely paired yet — never guess among generated enemies
		}

		var unboundStates = _enemies.Enemies.Where(s => !_entityById.ContainsKey(s.EntityId)).ToList();
		if (unboundStates.Count == 0)
		{
			return;
		}

		var candidates = FindAnimals().Where(e => _runtimeAnimalCopies.Contains(e) && !_idByEntity.ContainsKey(e)).ToList();
		var statePositions = unboundStates.Select(s => s.Position).ToList();
		var candidatePositions = candidates.Select(e => new NetVector2(e.transform.position.x, e.transform.position.y)).ToList();
		if (!EnemyRuntimeSpawnArbitration.TryPairByPosition(statePositions, candidatePositions, out var pairs))
		{
			return;
		}

		for (var i = 0; i < pairs.Count; i++)
		{
			var state = unboundStates[pairs[i].StateIndex];
			Bind(candidates[pairs[i].CandidateIndex], state.EntityId, runtimeSpawn: false);
			Freeze(candidates[pairs[i].CandidateIndex]);
		}

		_log.LogInformation("[Enemy] bound {Count} runtime enemy copies to host ids.", pairs.Count);
	}
}
