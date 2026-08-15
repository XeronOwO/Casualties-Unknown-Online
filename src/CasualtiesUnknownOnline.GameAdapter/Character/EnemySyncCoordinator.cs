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
internal sealed partial class EnemySyncCoordinator(
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
	private readonly HashSet<NetworkEntityId> _runtimeEnemyIds = []; // host: ids allocated after the initial deterministic mapping (runtime spawns)
	private readonly HashSet<BuildingEntity> _runtimeAnimalCopies = []; // guest: animals created at runtime (never the generated baseline — the pairing must not steal a generated copy)
	private bool _mappingEstablished;
	private bool _guestFrozen; // guest: animals frozen at generation finish (before they move, so the pairing uses the spawn positions)

	internal void BindToSession()
	{
		_enemies.EnemySnapshotReceived += OnEnemySnapshotReceived;
		_enemies.EnemyStateReceived += OnEnemyStateReceived;
		_enemies.EnemyBiteReceived += OnEnemyBiteReceived;
		_enemies.EnemyAttackReceived += OnEnemyAttackReceived;
		_enemies.EnemyLungeReceived += OnEnemyLungeReceived;
	}

	internal void Unbind()
	{
		_enemies.EnemySnapshotReceived -= OnEnemySnapshotReceived;
		_enemies.EnemyStateReceived -= OnEnemyStateReceived;
		_enemies.EnemyBiteReceived -= OnEnemyBiteReceived;
		_enemies.EnemyAttackReceived -= OnEnemyAttackReceived;
		_enemies.EnemyLungeReceived -= OnEnemyLungeReceived;
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
		return new EnemyEntity(id)
		{
			Position = new NetVector2(entity.transform.position.x, entity.transform.position.y),
			Velocity = rb != null ? new NetVector2(rb.velocity.x, rb.velocity.y) : NetVector2.Zero,
			Rotation = entity.transform.eulerAngles.z,
			Health = entity.health,
			Stunned = false, // presentation flags land once the per-enemy stun state is wired (SpiderHandler.stunTime etc.)
			PrefabId = entity.id,
			RuntimeSpawned = runtimeSpawn,
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

	// ---- Host-ordered enemy attacks (the dedicated command — never the snapshot) ----

	private void OnEnemyAttackReceived(EnemyAttackMsg msg)
	{
		if (!_session.SessionActive || _session.Role != SessionRole.Guest)
		{
			return;
		}

		if (!_entityById.TryGetValue(msg.EnemyId.ToNetworkEntityId(), out var entity) || entity == null) // Unity object — ==
		{
			_log.LogWarning("[Enemy] attack {Kind} arrived for unknown enemy {Enemy} — the snapshot binding may not have arrived yet; command dropped.",
				msg.Kind, msg.EnemyId.ToNetworkEntityId());
			return;
		}

		switch (msg.Kind)
		{
			case EnemyAttackKind.SpiderBite:
				ApplyHostSpiderBite(entity, msg);
				break;
			case EnemyAttackKind.CrystalLunge:
				ApplyHostCrystalLunge(entity, msg);
				break;
			default:
				_log.LogWarning("[Enemy] unknown attack kind {Kind} for enemy {Enemy} — dropped.", msg.Kind, msg.EnemyId.ToNetworkEntityId());
				break;
		}
	}

	/// <summary>
	/// Apply the host-ordered spider bite to the LOCAL body using the frozen
	/// copy's own SpiderHandler (same prefab values, same DamageLimb virtual
	/// dispatch). Replicates CheckForLimbDamage's non-collision side effects
	/// (SpiderHandler.cs:148-160) around DamageLimb; the EnemyBitePatches
	/// postfix on DamageLimb reports the post-bite terminal state back to the
	/// host — the command and its report are the dedicated event chain.
	/// </summary>
	private void ApplyHostSpiderBite(BuildingEntity entity, EnemyAttackMsg msg)
	{
		var spider = entity.GetComponentInChildren<SpiderHandler>();
		var body = LocalBody();
		if (spider == null || body == null) // Unity objects — ==
		{
			_log.LogWarning("[Enemy] spider bite {Enemy} could not be applied — attacker/victim body missing.", msg.EnemyId.ToNetworkEntityId());
			return;
		}

		var limb = SelectLimb(body, msg.LimbIndex, entity.transform.position);
		if (limb == null)
		{
			_log.LogWarning("[Enemy] spider bite {Enemy} has no non-dismembered limb — dropped.", msg.EnemyId.ToNetworkEntityId());
			return;
		}

		Sound.Play(spider.biteSound, entity.transform.position, false, true, null, 1f, 1f, false, false);
		limb.body.eyeScareTime = 5f;
		limb.body.talker.Talk(Locale.GetCharacter("hitbycreature"), null, false, true);
		limb.body.happiness -= spider.happinessLoss;
		spider.PlayThreatMusic();
		spider.DamageLimb(limb); // the EnemyBite report fires from the DamageLimb postfix
		if (spider.hitConnected)
		{
			foreach (var connected in limb.connectedLimbs)
			{
				spider.DamageLimb(connected);
			}
		}

		_log.LogInformation("[Enemy] applied host spider bite {Enemy} to local limb {Limb}.", msg.EnemyId.ToNetworkEntityId(), limb);
	}

	/// <summary>
	/// Apply the host-ordered crystal lunge to the LOCAL body, reproducing
	/// CrystalEnemy.Lunge's player-damage branch exactly
	/// (CrystalEnemy.cs:143-156): closest non-dismembered limb, the same
	/// armor-reduced damage constants and body reactions. The post-lunge
	/// terminal state is reported as the dedicated EnemyLunge event.
	/// </summary>
	private void ApplyHostCrystalLunge(BuildingEntity entity, EnemyAttackMsg msg)
	{
		var crystal = entity.GetComponentInChildren<CrystalEnemy>();
		var body = LocalBody();
		if (crystal == null || body == null) // Unity objects — ==
		{
			_log.LogWarning("[Enemy] crystal lunge {Enemy} could not be applied — attacker/victim body missing.", msg.EnemyId.ToNetworkEntityId());
			return;
		}

		var limb = SelectLimb(body, msg.LimbIndex, entity.transform.position);
		if (limb == null)
		{
			_log.LogWarning("[Enemy] crystal lunge {Enemy} has no non-dismembered limb — dropped.", msg.EnemyId.ToNetworkEntityId());
			return;
		}

		var armorReduction = limb.GetArmorReduction();
		limb.DamageWearables(0.4f);
		limb.muscleHealth -= 35f / armorReduction;
		limb.skinHealth -= 50f / armorReduction;
		limb.pain += 60f / armorReduction;
		limb.bleedAmount += 15f / armorReduction;
		body.adrenaline += 70f;
		body.stamina = 100f;
		body.eyePanicTime = 0.5f;
		body.Scream();
		body.Ragdoll();
		body.DoGoreSound();
		Sound.Play("crystalenemylaugh", entity.transform.position, true, true, null, 1f, 1f, false, false);

		var limbIndex = LimbIndexOf(body, limb);
		var limbMsg = _mapper.Map<CharacterLimbMsg>(limb);
		limbMsg.Index = limbIndex;
		_enemies.SendEnemyLunge(new EnemyLungeMsg
		{
			VictimSteamId = _session.LocalSteamId,
			Limb = limbMsg,
			Adrenaline = body.adrenaline,
			Stamina = body.stamina,
		});
		_log.LogInformation("[Enemy] applied host crystal lunge {Enemy} to local limb {Limb}.", msg.EnemyId.ToNetworkEntityId(), limbIndex);
	}

	private void OnEnemyLungeReceived(ulong sender, EnemyLungeMsg msg) => _characterData.ApplyEnemyLunge(msg);

	private static Limb? SelectLimb(Body body, int limbIndex, Vector3 enemyPosition)
	{
		if (limbIndex >= 0 && limbIndex < body.limbs.Length)
		{
			var indexed = body.limbs[limbIndex];
			if (indexed != null && !indexed.dismembered) // Unity object — ==
			{
				return indexed;
			}
		}

		var closest = body.GetClosestLimb(enemyPosition);
		return closest != null && !closest.dismembered ? closest : null; // Unity object — ==
	}

	private static Body? LocalBody()
	{
		var playerCamera = PlayerCamera.main;
		return playerCamera != null ? playerCamera.body : null; // Unity objects — ==
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
