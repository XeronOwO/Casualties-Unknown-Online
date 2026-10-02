using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The Unity side of the host-authoritative enemy stream. Host: captures the
/// simulated animal entities (BuildingEntity.animal), assigns ids in the
/// deterministic <see cref="EnemySpawnArbitration"/> order, records each one's
/// bind-time spawn anchor (<see cref="EnemyStateCapture"/> owns that state and
/// the native read) and publishes their presentation state. Guest: binds
/// its locally generated copies to the host's ids on the world-entry snapshot
/// AND on the 60 s in-session repair — spawn-anchor pairing against the UNBOUND
/// copies only, so a late snapshot whose host animals have already wandered
/// still pairs a member that has not bound its set yet, while the copies whose
/// ids are already decided are never re-paired (a re-pair compares the host's
/// bind-time anchor against the copy's current, already-driven position, so it
/// can only fail) — and drives
/// the frozen copies from the 20 Hz batch. No
/// enemy simulation on the guest — same pattern as the player render clones
/// (RemoteBodyDriver). Both roles separate the generation baseline from a
/// runtime spawn through the ONE rule <see cref="OnAnimalInstantiated"/> applies
/// at the entity's own Start; a "created after my first capture" proxy cannot
/// hold, because the host's baseline is established whenever its first frame
/// runs, not when generation ends. Also the enemy-bite side: reports the local
/// victim's post-bite state (EnemyBite event) and applies the received bites to
/// the victim's clone.
/// </summary>
internal sealed partial class EnemySyncCoordinator
{
	private readonly ISessionControl _session;
	private readonly EnemySyncService _enemies;
	private readonly ILogger<EnemySyncCoordinator> _log;
	private readonly EnemyCombatReplay _combat;
	private readonly EnemyPresentationApplier _presentation;

	internal EnemySyncCoordinator(
		ISessionControl session,
		EnemySyncService enemies,
		IMapper mapper,
		CharacterDataSync characterData,
		ILogger<EnemySyncCoordinator> log)
	{
		_session = session;
		_enemies = enemies;
		_log = log;
		_capture = new EnemyStateCapture(log);
		_combat = new EnemyCombatReplay(session, enemies, mapper, characterData, FindEntityById, log);
		_presentation = new EnemyPresentationApplier(log);
	}

	private readonly Dictionary<BuildingEntity, NetworkEntityId> _idByEntity = [];
	private readonly Dictionary<NetworkEntityId, BuildingEntity> _entityById = [];
	private readonly EnemyStateCapture _capture; // the host's native-to-DTO capture half: the per-frame state read plus the bind-time spawn anchors (extracted 2026-10-02 with the class on the architecture watchlist)
	private readonly HashSet<BuildingEntity> _runtimeAnimals = []; // BOTH roles: animals whose Start ran with the session active and generation finished — the ONE runtime-spawn rule, so the host's facts and the guest's pairing candidates cannot disagree (20261002-e: the old "after the first capture" proxy shipped all 80 generation enemies, 0 host vs 80 guest, mapping=false)
	private bool _mappingEstablished;
	private bool _guestFrozen; // guest: animals frozen at generation finish (before they move, so the pairing uses the spawn positions)

	internal void BindToSession()
	{
		_enemies.EnemySnapshotReceived += OnEnemySnapshotReceived;
		_enemies.EnemyStateReceived += OnEnemyStateReceived;
		_enemies.EnemyRemovedReceived += OnEnemyRemoved;
		_enemies.EnemyBiteReceived += _combat.OnEnemyBiteReceived;
		_enemies.EnemyAttackReceived += _combat.OnEnemyAttackReceived;
		_enemies.EnemyLungeReceived += _combat.OnEnemyLungeReceived;
	}

	internal void Unbind()
	{
		_enemies.EnemySnapshotReceived -= OnEnemySnapshotReceived;
		_enemies.EnemyStateReceived -= OnEnemyStateReceived;
		_enemies.EnemyRemovedReceived -= OnEnemyRemoved;
		_enemies.EnemyBiteReceived -= _combat.OnEnemyBiteReceived;
		_enemies.EnemyAttackReceived -= _combat.OnEnemyAttackReceived;
		_enemies.EnemyLungeReceived -= _combat.OnEnemyLungeReceived;
		_idByEntity.Clear();
		_entityById.Clear();
		_capture.Clear();
		_presentation.Clear();
		_runtimeAnimals.Clear();
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

	/// <summary>Guest side: the host explicitly removed an enemy aggregate —
	/// drop the local binding and destroy the frozen copy. This is the lifecycle
	/// counterpart of the update-only 20 Hz state stream.</summary>
	private void OnEnemyRemoved(NetworkEntityId id)
	{
		if (!_entityById.TryGetValue(id, out var entity))
		{
			return;
		}

		_idByEntity.Remove(entity);
		_entityById.Remove(id);
		_runtimeAnimals.Remove(entity);
		_presentation.Forget(id);
		if (entity != null) // Unity object — ==
		{
			Object.Destroy(entity.gameObject);
			_log.LogInformation("[Enemy] guest destroyed removed enemy {Enemy}.", id);
		}
	}

	private BuildingEntity? FindEntityById(NetworkEntityId id) =>
		_entityById.TryGetValue(id, out var entity) ? entity : null;

	/// <summary>
	/// Patch-bridge entry: an animal BuildingEntity started OUTSIDE world
	/// generation with the session up — a runtime spawn. BOTH roles classify it
	/// HERE, through this one guard, so the host's backfill facts and the guest's
	/// pairing candidates can never disagree about which animals generation
	/// produced. The old host rule ("appeared after the first capture") did
	/// disagree whenever the baseline was captured before the generation output
	/// existed: batch 20261002-e shipped all 80 generation enemies as runtime
	/// spawns, and the guest's pairing then read 0 host vs 80 guest generated
	/// copies, leaving every generated enemy unbound. The guest additionally
	/// freezes its copy at the spawn position, so the runtime pairing sees it
	/// before its AI/physics can move it and the host's 20 Hz state can drive it.
	/// </summary>
	internal void OnAnimalInstantiated(BuildingEntity entity)
	{
		if (!EnemyRuntimeSpawnArbitration.IsRuntimeSpawn(_session.SessionActive, HarmonyTraverse.IsGenerating()))
		{
			return;
		}

		_runtimeAnimals.Add(entity);
		if (_session.Role == SessionRole.Guest)
		{
			Freeze(entity);
		}
	}

	// ---- Host capture ----

	private void CaptureHostEnemies()
	{
		var animals = FindAnimals();
		_runtimeAnimals.RemoveWhere(e => e == null); // Unity object — == (a destroyed runtime animal must not hold a set entry for the rest of the session)
		EnsureMapping(animals);

		var states = new List<EnemyEntity>(animals.Count);
		foreach (var entity in animals)
		{
			var id = _idByEntity[entity];
			// The runtime flag is the ONE classification recorded at the entity's
			// own Start (OnAnimalInstantiated) — never "appeared after my first
			// capture", which is what turned every generation enemy into a
			// backfill fact in batch 20261002-e.
			states.Add(_capture.Capture(entity, id, runtimeSpawn: _runtimeAnimals.Contains(entity)));
		}

		_enemies.PublishEnemyStates(states);
	}

	/// <summary>Assign ids on the first capture in the deterministic (x, y) order; later captures keep the mapping and give fresh ids to any animal that appeared since. The runtime-spawn decision is NOT made here — <see cref="OnAnimalInstantiated"/> records it, both roles through the same guard — this method only allocates identity.</summary>
	private void EnsureMapping(List<BuildingEntity> animals)
	{
		if (_mappingEstablished)
		{
			foreach (var entity in animals)
			{
				if (!_idByEntity.ContainsKey(entity))
				{
					var id = _enemies.AllocateEnemyId();
					Bind(entity, id);
					_log.LogInformation("[Enemy] host bound {Kind} {Id} (prefab {Prefab}).",
						_runtimeAnimals.Contains(entity) ? "runtime spawn" : "generation animal", id, entity.id);
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
		_log.LogInformation("[Enemy] host enemy baseline established over {Count} animals.", sorted.Count);
	}

	/// <summary>Bind one entity to its id. The two identity tables serve both roles (host capture and guest binding); on the host the bind also records the spawn anchor the snapshot pairs on, which is why it lives in <see cref="EnemyStateCapture"/>.</summary>
	private void Bind(BuildingEntity entity, NetworkEntityId id)
	{
		_idByEntity[entity] = id;
		_entityById[id] = entity;

		if (_session.Role == SessionRole.Host)
		{
			_capture.RecordAnchor(entity, new NetVector2(entity.transform.position.x, entity.transform.position.y));
		}
	}

	private static List<BuildingEntity> FindAnimals() =>
		[.. Object.FindObjectsOfType<BuildingEntity>().Where(e => e.animal)];

	// ---- Guest: freeze at generation finish, then bind on the snapshot ----

	/// <summary>
	/// Freeze the guest's animal copies the moment generation finishes — BEFORE
	/// their AI moves them. The pairing key is the spawn position and only the
	/// HOST records it as an anchor, so the copies must still be at their spawn
	/// spots when a snapshot (entry or the 60 s repair) pairs them; freezing
	/// early also stops the guest from simulating (host-authoritative).
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
		// deterministic generation baseline — pair it on the host's bind-time
		// SPAWN anchor, never on the live position, and pair only the copies that
		// are still UNBOUND: a copy that already carries a host id has its
		// identity, and re-pairing the bound ones compares the host's anchor
		// against their CURRENT position (the 20 Hz drive moved them), failing the
		// whole all-or-nothing set on every 60 s repair — batch `20261002-f`
		// row 1: 4/4 cycles logged `generation spawn pairing failed` and
		// `mapping=False` on both guests and switched off the runtime-spawn bind.
		// The copies below have been frozen at their spawn spots since
		// generation, so the repair key is the side that does NOT move. (The
		// anchor is the host's FIRST-BIND position, which is its spawn position
		// only while generation has just finished: an animal the host first bound
		// after it moved is a pre-existing limit of this pairing, unchanged here —
		// `EnemyStateCapture` can only report the missing-anchor case loudly,
		// there is no reference to compare a late anchor against.)
		var comparer = Comparer<NetVector2>.Create(EnemySpawnArbitration.Compare);
		var generatedHost = hostStates
			.Where(s => !runtimeIds.Contains(s.EntityId))
			.OrderBy(s => s.SpawnPosition, comparer)
			.ToList();
		var generatedGuest = FindAnimals()
			.Where(e => EnemySpawnArbitration.IsRepairCandidate(
				hasHostId: _idByEntity.ContainsKey(e),
				isRuntimeAnimal: _runtimeAnimals.Contains(e)))
			.OrderBy(e => new NetVector2(e.transform.position.x, e.transform.position.y), comparer)
			.ToList();

		// An empty candidate set is NOT a divergence: it means nothing is left to
		// pair, so the pass preserves the baseline instead of re-pairing (and
		// failing on) copies whose identity is already decided.
		var unboundGuestCopies = generatedGuest.Count;
		var generatedPaired = false;
		if (generatedHost.Count != 0 && unboundGuestCopies != 0)
		{
			var hostPositions = generatedHost.Select(e => e.Position).ToList();
			var guestPositions = generatedGuest.Select(e => new NetVector2(e.transform.position.x, e.transform.position.y)).ToList();
			generatedPaired = EnemySpawnArbitration.TryPair(hostPositions, guestPositions, out _);
			if (generatedPaired)
			{
				for (var i = 0; i < generatedHost.Count; i++)
				{
					Bind(generatedGuest[i], generatedHost[i].EntityId);
					Freeze(generatedGuest[i]);
				}
			}
		}

		if (!generatedPaired && unboundGuestCopies != 0)
		{
			_log.LogWarning("[Enemy] generation spawn pairing failed ({Host} host vs {Guest} guest generated enemies) — generated copies stay local (generation divergence); runtime spawns are still bound.",
				generatedHost.Count, unboundGuestCopies);
		}

		_mappingEstablished = EnemySpawnArbitration.ShouldRepairGenerationBaseline(
			_mappingEstablished, generatedPaired, unboundGuestCopies);
		ApplyAllStates();
		// The generated reading is the copies this pass can ASSERT as bound (host
		// facts minus the candidates that still have no id), not the copies paired
		// this cycle: a steady-state repair pairs nothing yet holds the whole
		// baseline, so `0 generated bound` there would read as an unbound set.
		_log.LogInformation("[Enemy] snapshot applied: {Generated} generated bound, {Runtime} runtime spawns, mapping={Mapping}.",
			EnemySpawnArbitration.AssertedBoundCopies(generatedHost.Count, unboundGuestCopies),
			runtimeSpawns.Count,
			_mappingEstablished);
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

		_presentation.RecordLocalDamage(id, damage);
	}

	// ---- Host-ordered enemy attacks / bites: delegated to EnemyCombatReplay ----

	internal void ReportLocalCrystalLunge(Limb limb) => _combat.ReportLocalCrystalLunge(limb);

	internal void ReportEnemyBite(Limb limb) => _combat.ReportEnemyBite(limb);
}
