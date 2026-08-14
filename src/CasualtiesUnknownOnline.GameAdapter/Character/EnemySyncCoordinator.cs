using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The Unity side of the host-authoritative enemy stream. Host: captures the
/// simulated animal entities (BuildingEntity.animal), assigns ids in the
/// deterministic <see cref="EnemySpawnArbitration"/> order and publishes their
/// presentation state. Guest: binds its locally generated copies to the host's
/// ids on the world-entry snapshot (position pairing) and drives the frozen
/// copies from the 20 Hz batch. No enemy simulation on the guest — same
/// pattern as the player render clones (RemoteBodyDriver).
/// </summary>
internal sealed class EnemySyncCoordinator(
	SessionService session,
	EnemySyncService enemies,
	ILogger<EnemySyncCoordinator> log)
{
	private readonly SessionService _session = session;
	private readonly EnemySyncService _enemies = enemies;
	private readonly ILogger<EnemySyncCoordinator> _log = log;

	private readonly Dictionary<BuildingEntity, NetworkEntityId> _idByEntity = [];
	private readonly Dictionary<NetworkEntityId, BuildingEntity> _entityById = [];
	private bool _mappingEstablished;

	internal void BindToSession()
	{
		_enemies.EnemySnapshotReceived += OnEnemySnapshotReceived;
		_enemies.EnemyStateReceived += OnEnemyStateReceived;
	}

	internal void Unbind()
	{
		_enemies.EnemySnapshotReceived -= OnEnemySnapshotReceived;
		_enemies.EnemyStateReceived -= OnEnemyStateReceived;
	}

	internal void Update()
	{
		if (_session.Role == SessionRole.Host && _session.SessionActive)
		{
			CaptureHostEnemies();
		}
	}

	// ---- Host capture ----

	private void CaptureHostEnemies()
	{
		var animals = FindAnimals();
		EnsureMapping(animals);

		var states = new List<EnemyEntity>(animals.Count);
		foreach (var entity in animals)
		{
			states.Add(Capture(entity, _idByEntity[entity]));
		}

		_enemies.PublishEnemyStates(states);
	}

	/// <summary>Assign ids on the first capture in the deterministic (x, y) order; later captures keep the mapping and give fresh ids only to newly spawned enemies.</summary>
	private void EnsureMapping(List<BuildingEntity> animals)
	{
		if (_mappingEstablished)
		{
			foreach (var entity in animals)
			{
				if (!_idByEntity.ContainsKey(entity))
				{
					Bind(entity, _enemies.AllocateEnemyId());
				}
			}

			return;
		}

		var comparer = Comparer<NetVector2>.Create(EnemySpawnArbitration.Compare);
		var sorted = animals
			.OrderBy(e => new NetVector2(e.transform.position.x, e.transform.position.y), comparer)
			.ToList();
		foreach (var entity in sorted)
		{
			Bind(entity, _enemies.AllocateEnemyId());
		}

		_mappingEstablished = true;
	}

	private void Bind(BuildingEntity entity, NetworkEntityId id)
	{
		_idByEntity[entity] = id;
		_entityById[id] = entity;
	}

	private static EnemyEntity Capture(BuildingEntity entity, NetworkEntityId id)
	{
		var rb = entity.GetComponent<Rigidbody2D>();
		return new EnemyEntity(id)
		{
			Position = new NetVector2(entity.transform.position.x, entity.transform.position.y),
			Velocity = rb != null ? new NetVector2(rb.velocity.x, rb.velocity.y) : NetVector2.Zero,
			Rotation = entity.transform.eulerAngles.z,
			Health = entity.health,
			Stunned = false, // presentation flags land once the per-enemy stun state is wired (SpiderHandler.stunTime etc.)
		};
	}

	private static List<BuildingEntity> FindAnimals() =>
		[.. UnityEngine.Object.FindObjectsOfType<BuildingEntity>().Where(e => e.animal)];

	// ---- Guest: bind + freeze on the world-entry snapshot ----

	private void OnEnemySnapshotReceived()
	{
		var animals = FindAnimals();
		var hostStates = _enemies.Enemies.ToList();
		if (hostStates.Count == 0)
		{
			return;
		}

		var comparer = Comparer<NetVector2>.Create(EnemySpawnArbitration.Compare);
		var hostSorted = hostStates.OrderBy(e => e.Position, comparer).ToList();
		var guestSorted = animals
			.OrderBy(e => new NetVector2(e.transform.position.x, e.transform.position.y), comparer)
			.ToList();

		var hostPositions = hostSorted.Select(e => e.Position).ToList();
		var guestPositions = guestSorted.Select(e => new NetVector2(e.transform.position.x, e.transform.position.y)).ToList();
		if (!EnemySpawnArbitration.TryPair(hostPositions, guestPositions, out _))
		{
			_log.LogWarning("[Enemy] spawn pairing failed ({Host} host vs {Guest} guest enemies) — enemy copies stay local (generation divergence).",
				hostSorted.Count, guestSorted.Count);
			return;
		}

		for (var i = 0; i < hostSorted.Count; i++)
		{
			var id = hostSorted[i].EntityId;
			var entity = guestSorted[i];
			Bind(entity, id);
			Freeze(entity);
		}

		_mappingEstablished = true;
		_log.LogInformation("[Enemy] bound {Count} enemy copies to the host ids.", hostSorted.Count);
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
		foreach (var state in _enemies.Enemies)
		{
			if (_entityById.TryGetValue(state.EntityId, out var entity) && entity != null) // Unity object — ==
			{
				Apply(entity, state);
			}
		}
	}

	private static void Apply(BuildingEntity entity, EnemyEntity state)
	{
		entity.transform.position = new Vector3(state.Position.X, state.Position.Y, entity.transform.position.z);
		entity.transform.rotation = Quaternion.Euler(0f, 0f, state.Rotation);
		entity.health = state.Health;
	}
}
