using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
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
internal sealed partial class EnemySyncCoordinator
{
	private readonly SessionService _session;
	private readonly EnemySyncService _enemies;
	private readonly ILogger<EnemySyncCoordinator> _log;
	private readonly EnemyCombatReplay _combat;

	internal EnemySyncCoordinator(
		SessionService session,
		EnemySyncService enemies,
		IMapper mapper,
		CharacterDataSync characterData,
		ILogger<EnemySyncCoordinator> log)
	{
		_session = session;
		_enemies = enemies;
		_log = log;
		_combat = new EnemyCombatReplay(session, enemies, mapper, characterData, FindEntityById, log);
	}

	private readonly Dictionary<BuildingEntity, NetworkEntityId> _idByEntity = [];
	private readonly Dictionary<NetworkEntityId, BuildingEntity> _entityById = [];
	private readonly Dictionary<NetworkEntityId, EnemyHealthReconcile> _healthReconcile = [];
	private readonly HashSet<NetworkEntityId> _runtimeEnemyIds = []; // host: ids allocated after the initial deterministic mapping (runtime spawns)
	private readonly HashSet<BuildingEntity> _runtimeAnimalCopies = []; // guest: animals created at runtime (never the generated baseline — the pairing must not steal a generated copy)
	private bool _mappingEstablished;
	private bool _guestFrozen; // guest: animals frozen at generation finish (before they move, so the pairing uses the spawn positions)

	internal void BindToSession()
	{
		_enemies.EnemySnapshotReceived += OnEnemySnapshotReceived;
		_enemies.EnemyStateReceived += OnEnemyStateReceived;
		_enemies.EnemyBiteReceived += _combat.OnEnemyBiteReceived;
		_enemies.EnemyAttackReceived += _combat.OnEnemyAttackReceived;
		_enemies.EnemyLungeReceived += _combat.OnEnemyLungeReceived;
	}

	internal void Unbind()
	{
		_enemies.EnemySnapshotReceived -= OnEnemySnapshotReceived;
		_enemies.EnemyStateReceived -= OnEnemyStateReceived;
		_enemies.EnemyBiteReceived -= _combat.OnEnemyBiteReceived;
		_enemies.EnemyAttackReceived -= _combat.OnEnemyAttackReceived;
		_enemies.EnemyLungeReceived -= _combat.OnEnemyLungeReceived;
		_idByEntity.Clear();
		_entityById.Clear();
		_healthReconcile.Clear();
		_runtimeEnemyIds.Clear();
		_runtimeAnimalCopies.Clear();
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

	/// <summary>Host side: the id of one captured enemy (the combat director resolves the EnemyAttack sender id).</summary>
	internal bool TryGetHostEnemyId(BuildingEntity entity, out NetworkEntityId id) =>
		_idByEntity.TryGetValue(entity, out id);

	private BuildingEntity? FindEntityById(NetworkEntityId id) =>
		_entityById.TryGetValue(id, out var entity) ? entity : null;

	/// <summary>
	/// Patch-bridge entry: an animal BuildingEntity started OUTSIDE world
	/// generation on the guest — a runtime spawn (local trigger or the peer's
	/// relay). Freeze it immediately at its spawn position so the runtime
	/// position pairing sees it before its AI/physics can move it; the host's
	/// 20 Hz state then drives the frozen copy.
	/// </summary>
	internal void OnAnimalInstantiated(BuildingEntity entity)
	{
		if (!_session.SessionActive || _session.Role != SessionRole.Guest || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		_runtimeAnimalCopies.Add(entity);
		Freeze(entity);
	}

	// ---- Host capture ----

	private void CaptureHostEnemies()
	{
		var animals = FindAnimals();
		EnsureMapping(animals);

		var states = new List<EnemyEntity>(animals.Count);
		foreach (var entity in animals)
		{
			var id = _idByEntity[entity];
			states.Add(Capture(entity, id, _runtimeEnemyIds.Contains(id)));
		}

		_enemies.PublishEnemyStates(states);
	}

	/// <summary>Assign ids on the first capture in the deterministic (x, y) order; later captures keep the mapping and give fresh ids only to newly spawned enemies (marked runtime — the late-joiner snapshot materializes them).</summary>
	private void EnsureMapping(List<BuildingEntity> animals)
	{
		if (_mappingEstablished)
		{
			foreach (var entity in animals)
			{
				if (!_idByEntity.ContainsKey(entity))
				{
					var id = _enemies.AllocateEnemyId();
					Bind(entity, id, runtimeSpawn: true);
					_log.LogInformation("[Enemy] host bound runtime spawn {Id} (prefab {Prefab}).", id, entity.id);
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
			Bind(entity, _enemies.AllocateEnemyId(), runtimeSpawn: false);
		}

		_mappingEstablished = true;
	}

	private void Bind(BuildingEntity entity, NetworkEntityId id, bool runtimeSpawn)
	{
		_idByEntity[entity] = id;
		_entityById[id] = entity;
		if (runtimeSpawn)
		{
			_runtimeEnemyIds.Add(id);
		}
	}

	private static EnemyEntity Capture(BuildingEntity entity, NetworkEntityId id, bool runtimeSpawn)
	{
		var rb = entity.GetComponent<Rigidbody2D>();
		var crystal = entity.GetComponentInChildren<CrystalEnemy>();
		var hasTint = false;
		NetColorRgba tint = default;
		var lightIntensity = 0f;
		if (crystal != null && CrystalEnemyTintAccess.TryRead(crystal, out var color, out lightIntensity)) // Unity object — ==
		{
			// The mimic's trigger-side SetColor (CrystalMimic.cs:32/46) painted
			// this copy; carry the EXACT post-jitter color (never a re-roll — the
			// SetColor jitter is per-side random) so the backfill can match it.
			hasTint = true;
			tint = new NetColorRgba(color.r, color.g, color.b, color.a);
		}

		return new EnemyEntity(id)
		{
			Position = new NetVector2(entity.transform.position.x, entity.transform.position.y),
			Velocity = rb != null ? new NetVector2(rb.velocity.x, rb.velocity.y) : NetVector2.Zero,
			Rotation = entity.transform.eulerAngles.z,
			Health = entity.health,
			Stunned = EnemyStunPresentation.IsStunned(entity),
			PrefabId = entity.id,
			RuntimeSpawned = runtimeSpawn,
			HasTint = hasTint,
			TintColor = tint,
			TintLightIntensity = lightIntensity,
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
		var hostStates = _enemies.Enemies.ToList();
		if (hostStates.Count == 0)
		{
			return;
		}

		var runtimeSpawns = _enemies.RuntimeSpawns.ToList();
		var runtimeIds = new HashSet<NetworkEntityId>(runtimeSpawns.Select(s => s.Id.ToNetworkEntityId()));
		MaterializeRuntimeSpawns(runtimeSpawns);

		// The runtime copies are bound/materialized; what remains is the
		// deterministic generation baseline — pair it exactly like before.
		var comparer = Comparer<NetVector2>.Create(EnemySpawnArbitration.Compare);
		var generatedHost = hostStates
			.Where(s => !runtimeIds.Contains(s.EntityId))
			.OrderBy(s => s.Position, comparer)
			.ToList();
		var generatedGuest = FindAnimals()
			.Where(e => !_runtimeAnimalCopies.Contains(e)
				&& !(_idByEntity.TryGetValue(e, out var boundId) && runtimeIds.Contains(boundId)))
			.OrderBy(e => new NetVector2(e.transform.position.x, e.transform.position.y), comparer)
			.ToList();

		var generatedPaired = generatedHost.Count == 0 && generatedGuest.Count == 0;
		if (!generatedPaired)
		{
			var hostPositions = generatedHost.Select(e => e.Position).ToList();
			var guestPositions = generatedGuest.Select(e => new NetVector2(e.transform.position.x, e.transform.position.y)).ToList();
			generatedPaired = EnemySpawnArbitration.TryPair(hostPositions, guestPositions, out _);
			if (generatedPaired)
			{
				for (var i = 0; i < generatedHost.Count; i++)
				{
					Bind(generatedGuest[i], generatedHost[i].EntityId, runtimeSpawn: false);
					Freeze(generatedGuest[i]);
				}
			}
		}

		if (!generatedPaired)
		{
			_log.LogWarning("[Enemy] generation spawn pairing failed ({Host} host vs {Guest} guest generated enemies) — generated copies stay local (generation divergence); runtime spawns are still bound.",
				generatedHost.Count, generatedGuest.Count);
		}

		_mappingEstablished = generatedPaired;
		ApplyAllStates();
		_log.LogInformation("[Enemy] snapshot applied: {Generated} generated bound, {Runtime} runtime spawns, mapping={Mapping}.",
			generatedPaired ? generatedHost.Count : 0, runtimeSpawns.Count, _mappingEstablished);
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

		if (EnemyStunPresentation.Apply(entity, state.Stunned))
		{
			_log.LogInformation("[Enemy] {Enemy} stun presentation -> {New}.", state.EntityId, state.Stunned);
		}
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

	// ---- Host-ordered enemy attacks / bites: delegated to EnemyCombatReplay ----

	internal void ReportLocalCrystalLunge(Limb limb) => _combat.ReportLocalCrystalLunge(limb);

	internal void ReportEnemyBite(Limb limb) => _combat.ReportEnemyBite(limb);
}
