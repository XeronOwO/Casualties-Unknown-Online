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
/// The Unity side of the host-authoritative enemy stream. The HOST's enemy table
/// is the roster: it allocates one id per simulated animal (generated and
/// runtime-spawned alike), records the position it first bound the animal at as
/// a match hint, and publishes each enemy's presentation state. The GUEST never
/// derives identity from its own generation: on the world-entry snapshot and on
/// the 60 s repair it ADOPTS its local copies against the roster row by row
/// (<see cref="EnemyRosterBinder"/>) — reuse the copy a row plausibly is,
/// materialize the row where the host has it when no copy can be reused, and
/// destroy local animals the roster does not contain. A row is never a whole-set
/// verdict, so one divergence cannot leave the session's enemies unbound.
/// <para>
/// This class owns the host's capture, the generation/lifecycle boundaries and
/// the freeze pump; the identity tables are <see cref="EnemyBindingTable"/>, the
/// host's native capture is <see cref="EnemyStateCapture"/>, the guest's
/// presentation write half is <see cref="EnemyPresentationApplier"/>, and the
/// per-row judgment is the pure <see cref="EnemyRosterAdoption"/>. Both roles
/// separate the generation baseline from a runtime spawn through the ONE rule
/// <see cref="OnAnimalInstantiated"/> applies at the entity's own Start — a
/// "created after my first capture" proxy cannot hold, because the host's
/// baseline is established whenever its first frame runs, not when generation
/// ends. Also the enemy-bite side: reports the local victim's post-bite state
/// (EnemyBite event) and applies the received bites to the victim's clone.
/// </para>
/// </summary>
internal sealed class EnemySyncCoordinator
{
	private readonly ISessionControl _session;
	private readonly EnemySyncService _enemies;
	private readonly ILogger<EnemySyncCoordinator> _log;
	private readonly EnemyCombatReplay _combat;
	private readonly EnemyPresentationApplier _presentation;
	private readonly EnemyBindingTable _bindings = new();
	private readonly EnemyRosterBinder _binder;

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
		_binder = new EnemyRosterBinder(enemies, _bindings, _runtimeAnimals, _localAnimals, _presentation, log);
	}

	private readonly EnemyStateCapture _capture; // the host's native-to-DTO capture half: the per-frame state read plus the match hints (extracted 2026-10-02 with the class on the architecture watchlist)
	private readonly HashSet<BuildingEntity> _runtimeAnimals = []; // BOTH roles: animals whose Start ran with the session active and generation finished — the ONE runtime-spawn rule, so the host's facts and the guest's adoption candidates cannot disagree (20261002-e: the old "after the first capture" proxy shipped all 80 generation enemies, 0 host vs 80 guest, mapping=false)
	private readonly HashSet<BuildingEntity> _localAnimals = []; // BOTH roles: every animal whose Start ran (the guest's adoption candidates; the scan is the safety net for a Start that has not run yet)
	private bool _mappingEstablished; // host: the first capture assigned the layer's ids
	private bool _guestFrozen; // guest: animals frozen at generation finish (before they move, so the adoption reuses the spawn-position hint)
	private bool _adoptionPending; // guest: a snapshot arrived while the world was generating/absent — apply it at the live-world edge

	internal void BindToSession()
	{
		_enemies.EnemySnapshotReceived += OnEnemySnapshotReceived;
		_enemies.EnemyStateReceived += OnEnemyStateReceived;
		_enemies.EnemyRemovedReceived += OnEnemyRemoved;
		_enemies.EnemyRosterReset += OnEnemyRosterReset;
		_enemies.EnemyBiteReceived += _combat.OnEnemyBiteReceived;
		_enemies.EnemyAttackReceived += _combat.OnEnemyAttackReceived;
		_enemies.EnemyLungeReceived += _combat.OnEnemyLungeReceived;
	}

	internal void Unbind()
	{
		_enemies.EnemySnapshotReceived -= OnEnemySnapshotReceived;
		_enemies.EnemyStateReceived -= OnEnemyStateReceived;
		_enemies.EnemyRemovedReceived -= OnEnemyRemoved;
		_enemies.EnemyRosterReset -= OnEnemyRosterReset;
		_enemies.EnemyBiteReceived -= _combat.OnEnemyBiteReceived;
		_enemies.EnemyAttackReceived -= _combat.OnEnemyAttackReceived;
		_enemies.EnemyLungeReceived -= _combat.OnEnemyLungeReceived;
		ClearRosterState();
	}

	internal void Update()
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			// Capture on EVERY frame (a runtime spawn must be reachable in the same
			// frame it starts), but publish the authoritative roster only over a
			// live, non-generating world: a partial table sent mid-generation would
			// tell a member to delete enemies its own generation has not produced.
			CaptureHostEnemies();
			if (EnemyRosterAdoption.CanPublishRoster(HarmonyTraverse.HasWorld, HarmonyTraverse.IsGenerating(), HarmonyTraverse.IsRegenerating()))
			{
				_enemies.MarkRosterReady();
			}
			else
			{
				_enemies.MarkRosterUnstable();
			}

			return;
		}

		FreezeOnGenerationComplete();
		if (_adoptionPending && EnemyRosterAdoption.CanPublishRoster(HarmonyTraverse.HasWorld, HarmonyTraverse.IsGenerating(), HarmonyTraverse.IsRegenerating()))
		{
			_adoptionPending = false;
			_binder.AdoptRoster();
		}
	}

	/// <summary>
	/// Called at the game's <c>GenerateWorld</c> boundary on BOTH roles: every id
	/// and hint names the layer being left, so the roster state starts empty for
	/// the layer being entered. The old layer's objects are being destroyed with
	/// the scene; nothing here destroys them.
	/// </summary>
	internal void OnWorldGenerationStarted()
	{
		ClearRosterState();
		_enemies.MarkRosterUnstable();
	}

	private void ClearRosterState()
	{
		_bindings.Clear();
		_binder.ForgetAll();
		_capture.Clear();
		_presentation.Clear();
		_mappingEstablished = false;
		_guestFrozen = false;
		_adoptionPending = false;
	}

	/// <summary>Host side: the id of one captured enemy (the combat director resolves the EnemyAttack sender id).</summary>
	internal bool TryGetHostEnemyId(BuildingEntity entity, out NetworkEntityId id) =>
		_bindings.TryGetId(entity, out id);

	/// <summary>Guest side: the host explicitly removed an enemy aggregate —
	/// drop the local binding and destroy the frozen copy. This is the lifecycle
	/// counterpart of the update-only 20 Hz state stream.</summary>
	private void OnEnemyRemoved(NetworkEntityId id)
	{
		var entity = _binder.Unbind(id);
		// The per-id damage-reconcile record goes with the id even when the object
		// is already gone; otherwise it survives until the layer reset.
		_presentation.Forget(id);
		if (entity == null) // Unity object — ==
		{
			return;
		}

		_runtimeAnimals.Remove(entity);
		_localAnimals.Remove(entity);
		Object.Destroy(entity.gameObject);
		_log.LogInformation("[Enemy] guest destroyed removed enemy {Enemy}.", id);
	}

	/// <summary>The host reset the enemy table for a new layer — the old bindings name the old scene's objects. The copies are gone with the scene; only the binding state is dropped here.</summary>
	private void OnEnemyRosterReset()
	{
		ClearRosterState();
		_log.LogInformation("[Enemy] guest dropped its enemy bindings: the host reset the roster for a new layer.");
	}

	private BuildingEntity? FindEntityById(NetworkEntityId id) => _bindings.Find(id);

	/// <summary>
	/// Patch-bridge entry: an animal BuildingEntity started with the session up —
	/// a runtime spawn if generation has finished, generation output otherwise.
	/// BOTH roles classify it HERE, through this one guard. The guest freezes a
	/// runtime copy before its AI/physics can move it, so the adoption's
	/// spawn-position hint still points at where the creation report placed it.
	/// </summary>
	internal void OnAnimalInstantiated(BuildingEntity entity)
	{
		_localAnimals.Add(entity);
		if (!EnemyRosterAdoption.IsRuntimeSpawn(_session.SessionActive, HarmonyTraverse.IsGenerating()))
		{
			return;
		}

		_runtimeAnimals.Add(entity);
		if (_session.Role == SessionRole.Guest)
		{
			EnemyRosterBinder.Freeze(entity);
		}
	}

	// ---- Host capture ----

	private void CaptureHostEnemies()
	{
		PruneDeadBindings();
		EnsureMapping(LocalAnimals());

		// The states come from the BOUND table, not from a one-frame scene scan: a
		// live enemy that is inactive or out of the scan's reach must keep its id
		// and its row. Only a Unity-destroyed object (pruned above) leaves the table,
		// which is the confirmed death the kernel projection turns into a tombstone.
		var states = new List<EnemyEntity>(_bindings.Count);
		foreach (var (id, entity) in _bindings.Bound)
		{
			if (entity == null) // Unity object — ==
			{
				continue;
			}

			states.Add(_capture.Capture(entity, id, runtimeSpawn: _runtimeAnimals.Contains(entity)));
		}

		_enemies.PublishEnemyStates(states);
	}

	/// <summary>Remove the bindings whose Unity object no longer exists (the confirmed death) and the destroyed entries of the tracking sets. A scan that misses a LIVE object never reaches here — that is why the states are built from the bound table instead of the scan.</summary>
	private void PruneDeadBindings()
	{
		var dead = _bindings.PruneDead();
		foreach (var (id, entity) in dead)
		{
			_capture.Forget(entity);
			_presentation.Forget(id);
			_localAnimals.Remove(entity);
			_runtimeAnimals.Remove(entity);
		}

		_localAnimals.RemoveWhere(entity => entity == null); // Unity object — ==
		_runtimeAnimals.RemoveWhere(entity => entity == null);
		if (dead.Count > 0)
		{
			_log.LogInformation("[Enemy] host dropped {Count} destroyed enemy binding(s).", dead.Count);
		}
	}

	/// <summary>
	/// Assign ids on the first capture in the deterministic (x, y) order; later
	/// captures keep the mapping and give fresh ids to any animal that appeared
	/// since. The runtime-spawn decision is NOT made here — <see
	/// cref="OnAnimalInstantiated"/> records it, both roles through the same guard
	/// — this method only allocates identity.
	/// </summary>
	private void EnsureMapping(List<BuildingEntity> animals)
	{
		if (_mappingEstablished)
		{
			foreach (var entity in animals)
			{
				if (_bindings.IsBound(entity))
				{
					continue;
				}

				var id = _enemies.AllocateEnemyId();
				BindHost(entity, id);
				_log.LogInformation("[Enemy] host bound {Kind} {Id} (prefab {Prefab}).",
					_runtimeAnimals.Contains(entity) ? "runtime spawn" : "generation animal", id, entity.id);
			}

			return;
		}

		var sorted = animals
			.OrderBy(static entity => new NetVector2(entity.transform.position.x, entity.transform.position.y), PositionComparer)
			.ToList();
		foreach (var entity in sorted)
		{
			BindHost(entity, _enemies.AllocateEnemyId());
		}

		_mappingEstablished = true;
		_log.LogInformation("[Enemy] host enemy baseline established over {Count} animals.", sorted.Count);
	}

	private static readonly IComparer<NetVector2> PositionComparer = Comparer<NetVector2>.Create(static (a, b) =>
	{
		var byX = a.X.CompareTo(b.X);
		return byX != 0 ? byX : a.Y.CompareTo(b.Y);
	});

	/// <summary>Bind one entity to its id on the host and record the match hint — where the host first saw the animal, a reuse hint only, never an identity.</summary>
	private void BindHost(BuildingEntity entity, NetworkEntityId id)
	{
		_bindings.Bind(entity, id);
		_capture.RecordMatchHint(entity, new NetVector2(entity.transform.position.x, entity.transform.position.y));
	}

	/// <summary>Every animal this side knows about: the tracked set plus a scene scan (a just-instantiated copy whose Start has not run yet, or an inert scan result). Prunes the destroyed entries.</summary>
	private List<BuildingEntity> LocalAnimals()
	{
		_localAnimals.RemoveWhere(entity => entity == null); // Unity object — ==
		var union = new HashSet<BuildingEntity>(_localAnimals);
		foreach (var entity in FindAnimals())
		{
			union.Add(entity);
		}

		foreach (var entity in union)
		{
			_localAnimals.Add(entity);
		}

		return [.. union];
	}

	private static List<BuildingEntity> FindAnimals() =>
		[.. Object.FindObjectsOfType<BuildingEntity>().Where(static e => e.animal)];

	// ---- Guest: freeze at generation finish, then adopt on the snapshot ----

	/// <summary>
	/// Freeze the guest's animal copies the moment generation finishes — BEFORE
	/// their AI moves them, so the adoption's match hint still points at where
	/// the copy was generated. Freezing early also stops the guest from
	/// simulating (host-authoritative); every adopted or materialized copy is
	/// frozen again individually.
	/// </summary>
	private void FreezeOnGenerationComplete()
	{
		if (_guestFrozen || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		var animals = LocalAnimals();
		if (animals.Count == 0)
		{
			return; // generation not finished yet (or a menu scene)
		}

		foreach (var entity in animals)
		{
			EnemyRosterBinder.Freeze(entity);
		}

		_guestFrozen = true;
		_log.LogInformation("[Enemy] guest froze {Count} enemy copies at generation finish (before they move).", animals.Count);
	}

	private void OnEnemySnapshotReceived()
	{
		if (!EnemyRosterAdoption.CanPublishRoster(HarmonyTraverse.HasWorld, HarmonyTraverse.IsGenerating(), HarmonyTraverse.IsRegenerating()))
		{
			// The member's world is still being generated, is in the regeneration
			// fade, or is not up: a roster applied now would materialize enemies the
			// member's own generation will produce a moment later. Apply the latest
			// snapshot at the settled-world edge instead — the service buffer
			// already holds it.
			_adoptionPending = true;
			_log.LogInformation("[Enemy] roster snapshot held: this side's world is generating, regenerating or not up; adoption runs when it is settled.");
			return;
		}

		_binder.AdoptRoster();
	}

	private void OnEnemyStateReceived()
	{
		// The live pass binds a stream-announced row to a local copy. Never run it
		// across a world transition: during the regeneration fade the host is
		// still broadcasting its new layer's rows while this side's scene is being
		// replaced, so a copy of the old scene could be bound and driven to the new
		// layer's positions. The snapshot adopted after the transition owns the
		// first pass; the next 20 Hz batch resumes the live pass.
		if (!EnemyRosterAdoption.CanPublishRoster(HarmonyTraverse.HasWorld, HarmonyTraverse.IsGenerating(), HarmonyTraverse.IsRegenerating()))
		{
			return;
		}

		_binder.OnStateReceived();
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

		if (!_bindings.TryGetId(entity, out var id))
		{
			return;
		}

		_presentation.RecordLocalDamage(id, damage);
	}

	// ---- Host-ordered enemy attacks / bites: delegated to EnemyCombatReplay ----

	internal void ReportLocalCrystalLunge(Limb limb) => _combat.ReportLocalCrystalLunge(limb);

	internal void ReportEnemyBite(Limb limb) => _combat.ReportEnemyBite(limb);
}
