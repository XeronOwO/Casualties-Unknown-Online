using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using MapsterMapper;
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
/// pattern as the player render clones (RemoteBodyDriver). Also the enemy-bite
/// side: reports the local victim's post-bite state (EnemyBite event) and
/// applies the received bites to the victim's clone.
/// </summary>
internal sealed class EnemySyncCoordinator(
	SessionService session,
	EnemySyncService enemies,
	IMapper mapper,
	CharacterDataSync characterData,
	ILogger<EnemySyncCoordinator> log)
{
	private readonly SessionService _session = session;
	private readonly EnemySyncService _enemies = enemies;
	private readonly IMapper _mapper = mapper;
	private readonly CharacterDataSync _characterData = characterData;
	private readonly ILogger<EnemySyncCoordinator> _log = log;

	private readonly Dictionary<BuildingEntity, NetworkEntityId> _idByEntity = [];
	private readonly Dictionary<NetworkEntityId, BuildingEntity> _entityById = [];
	private readonly Dictionary<NetworkEntityId, EnemyHealthReconcile> _healthReconcile = [];
	private bool _mappingEstablished;
	private bool _guestFrozen; // guest: animals frozen at generation finish (before they move, so the pairing uses the spawn positions)

	internal void BindToSession()
	{
		_enemies.EnemySnapshotReceived += OnEnemySnapshotReceived;
		_enemies.EnemyStateReceived += OnEnemyStateReceived;
		_enemies.EnemyBiteReceived += OnEnemyBiteReceived;
	}

	internal void Unbind()
	{
		_enemies.EnemySnapshotReceived -= OnEnemySnapshotReceived;
		_enemies.EnemyStateReceived -= OnEnemyStateReceived;
		_enemies.EnemyBiteReceived -= OnEnemyBiteReceived;
		_idByEntity.Clear();
		_entityById.Clear();
		_healthReconcile.Clear();
		_mappingEstablished = false;
		_guestFrozen = false;
	}

	internal void Update()
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			CaptureHostEnemies();
		}
		else
		{
			FreezeOnGenerationComplete();
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

	// ---- Guest: freeze at generation finish, then bind on the snapshot ----

	/// <summary>
	/// Freeze the guest's animal copies the moment generation finishes — BEFORE
	/// their AI moves them. The pairing key is the spawn position, so the copies
	/// must still be at their spawn spots when the host's snapshot arrives;
	/// freezing early also stops the guest from simulating (host-authoritative).
	/// </summary>
	private void FreezeOnGenerationComplete()
	{
		if (_guestFrozen || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		var animals = FindAnimals();
		if (animals.Count == 0)
		{
			return; // generation not finished yet (or a menu scene)
		}

		foreach (var entity in animals)
		{
			Freeze(entity);
		}

		_guestFrozen = true;
		_log.LogInformation("[Enemy] guest froze {Count} enemy copies at generation finish (before they move).", animals.Count);
	}

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

	private void Apply(BuildingEntity entity, EnemyEntity state)
	{
		entity.transform.position = new Vector3(state.Position.X, state.Position.Y, entity.transform.position.z);
		entity.transform.rotation = Quaternion.Euler(0f, 0f, state.Rotation);
		// A local attack drops this copy's health for immediate feedback, but the
		// host's batch does not yet include the in-flight report — reconciling
		// against the pending local damage keeps that drop visible instead of
		// flashing back up for one round-trip.
		entity.health = _healthReconcile.TryGetValue(state.EntityId, out var reconcile)
			? reconcile.Reconcile(state.Health)
			: state.Health;
	}

	/// <summary>
	/// A local attack damaged a frozen enemy copy (Body.Attack → the copy's
	/// health dropped before the report reaches the host): record the damage as
	/// pending so the next host batch does not revert it. Host side and untracked
	/// entities (non-enemies, or before the snapshot binding) are a no-op.
	/// </summary>
	internal void RecordLocalAttack(BuildingEntity entity, float damage)
	{
		if (_session.Role != SessionRole.Guest || damage <= 0f)
		{
			return;
		}

		if (!_idByEntity.TryGetValue(entity, out var id))
		{
			return;
		}

		if (!_healthReconcile.TryGetValue(id, out var reconcile))
		{
			reconcile = new EnemyHealthReconcile();
			_healthReconcile[id] = reconcile;
		}

		reconcile.RecordLocalDamage(damage);
	}

	// ---- Enemy bite (the dedicated trigger — never the 1 Hz snapshot) ----

	/// <summary>
	/// The local player was bitten (the game's DamageLimb already ran on the
	/// local body): capture the post-bite terminal state and send it as the
	/// dedicated EnemyBite event — guest → host report, host → guest broadcast
	/// (accept-first, no distance/legitimacy validation).
	/// </summary>
	internal void ReportEnemyBite(Limb limb)
	{
		if (!_session.SessionActive || limb.body == null) // Unity object — ==
		{
			return;
		}

		var body = limb.body;
		var limbIndex = LimbIndexOf(body, limb);
		if (limbIndex < 0)
		{
			return; // not a limb of the local body — nothing to report
		}

		var limbMsg = _mapper.Map<CharacterLimbMsg>(limb);
		limbMsg.Index = limbIndex;

		_enemies.SendEnemyBite(new EnemyBiteMsg
		{
			VictimSteamId = _session.LocalSteamId,
			Limb = limbMsg,
			VenomTotal = body.venomTotal,
			Adrenaline = body.adrenaline,
			Happiness = body.happiness,
		});
	}

	private void OnEnemyBiteReceived(ulong sender, EnemyBiteMsg msg) => _characterData.ApplyEnemyBite(msg);

	private static int LimbIndexOf(Body body, Limb limb)
	{
		for (var i = 0; i < body.limbs.Length; i++)
		{
			if (body.limbs[i] == limb) // Unity object — ==
			{
				return i;
			}
		}

		return -1;
	}
}
