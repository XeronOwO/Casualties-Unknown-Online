using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using MapsterMapper;
using Microsoft.Extensions.Logging;

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
/// <see cref="OnAnimalInstantiated"/> applies at the entity's own Start, over the
/// layer's generation-finished EDGE — the set of animals that existed when the
/// world settled — never the <c>generatingWorld</c> flag read at that Start (a
/// dense world's deferred Start calls read as runtime spawns, batch
/// `20261021-f`). Also the enemy-bite side: reports the local victim's post-bite
/// state (EnemyBite event) and applies the received bites to the victim's clone.
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
	private readonly HashSet<BuildingEntity> _runtimeAnimals = []; // BOTH roles: animals created AFTER this layer's generation-finished edge — the ONE runtime-spawn rule, so the host's facts and the guest's adoption candidates cannot disagree (20261002-e: the old "after the first capture" proxy shipped all 80 generation enemies, 0 host vs 80 guest, mapping=false)
	private readonly HashSet<BuildingEntity> _localAnimals = []; // BOTH roles: every animal this side has tracked — a Start, or a copy an earlier tracked∪scan union wrote back (the guest's adoption candidates and the host's bind source)
	private readonly HashSet<BuildingEntity> _generationAnimals = []; // BOTH roles: animals that existed when the layer's generation-finished edge was observed, plus every Start that ran before it — the generation-output fact (20261021-f: the generating flag read at Start made 873 of 956 generation animals read as runtime spawns)
	private bool _generationEdgeObserved; // BOTH roles: this layer's settled-world edge has been recorded; before it, nothing can be a runtime spawn
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
			UpdateHost();
			return;
		}

		UpdateGuest();
	}

	/// <summary>
	/// Host frame pump: capture and publish the authoritative roster — but ONLY
	/// over a settled world. A capture that runs while the layer generates binds
	/// animals the world-entry layer reset must then drop, and those dropped rows
	/// are what makes both members' entry adoption start from an empty candidate
	/// set (batch `20261021-f`, row 1: 83 rows published during generation, cleared
	/// at entry, 946 copies built on a 956-row world). The verdict is the SAME one
	/// the publication uses, so capture and publication cannot disagree about the
	/// roster being complete.
	/// </summary>
	private void UpdateHost()
	{
		if (!WorldIsSettled())
		{
			_enemies.MarkRosterUnstable();
			return;
		}

		// The animals that exist NOW are this layer's generation output, whatever
		// their Start timing; record the edge before the capture classifies them.
		MarkAnimalsCreatedByGeneration();
		CaptureHostEnemies();
		_enemies.MarkRosterReady();
	}

	/// <summary>
	/// Guest frame pump: freeze the local copies at generation finish and apply the
	/// snapshot held across the transition. The settled-world edge is also where
	/// this side learns which animals its own generation created — the fact the
	/// runtime-spawn classification reads instead of the generating flag at Start.
	/// </summary>
	private void UpdateGuest()
	{
		var settled = WorldIsSettled();
		if (settled)
		{
			MarkAnimalsCreatedByGeneration();
		}

		FreezeOnGenerationComplete();
		if (_adoptionPending && settled)
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
		_generationAnimals.Clear();
		_generationEdgeObserved = false;
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
		_binder.DestroyCopy(entity);
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
	/// The game's settled-world verdict — one expression for the capture gate, the
	/// generation-edge pass, the adoption gate and the live pass, so no two of them
	/// can disagree about whether the layer's roster is complete.
	/// </summary>
	private static bool WorldIsSettled() =>
		EnemyRosterAdoption.CanPublishRoster(HarmonyTraverse.HasWorld, HarmonyTraverse.IsGenerating(), HarmonyTraverse.IsRegenerating());

	/// <summary>
	/// Record the generation-finished edge ONCE per layer: every animal in the
	/// scene at the first settled frame is generation output, whatever its
	/// <c>Start</c> timing. A dense world's generator leaves hundreds of Start
	/// calls to run over the seconds after <c>generatingWorld</c> flips, so the
	/// flag read at Start is not the creation fact (batch `20261021-f`: 291 → 873
	/// runtime spawns on a 956-animal layer of generation output).
	/// </summary>
	private void MarkAnimalsCreatedByGeneration()
	{
		if (_generationEdgeObserved)
		{
			return;
		}

		var animals = LocalAnimals();
		foreach (var entity in animals)
		{
			_generationAnimals.Add(entity);
		}

		_generationEdgeObserved = true;
		_log.LogInformation("[Enemy] generation finished: {Count} animal(s) exist at the settled edge and are this layer's generation output — a later Start does not make them runtime spawns.",
			animals.Count);
	}

	/// <summary>
	/// Patch-bridge entry: an animal BuildingEntity started with the session up.
	/// The classification is the generation's finished EDGE, read through the ONE
	/// rule both roles share — NEVER the generating flag at this Start. Returns
	/// whether the animal is this layer's generation output: the entity-creation
	/// channel reads that same verdict (<see cref="GameAdapterBridge.OnEntityInstantiated"/>)
	/// so a deferred Start is not reported as a runtime creation and materialized a
	/// second time on every member. The guest freezes a runtime copy before its
	/// AI/physics can move it, so the adoption's spawn-position hint still points at
	/// where the creation report placed it.
	/// </summary>
	internal bool OnAnimalInstantiated(BuildingEntity entity)
	{
		_localAnimals.Add(entity);
		if (!EnemyRosterAdoption.IsRuntimeSpawn(_session.SessionActive, _generationEdgeObserved, _generationAnimals.Contains(entity)))
		{
			// Generation output, or a Start that ran before the edge was recorded:
			// either way the animal belongs to this layer's generation set, so the
			// scan at the edge (and any later pass) cannot reclassify it.
			_generationAnimals.Add(entity);
			return true;
		}

		_runtimeAnimals.Add(entity);
		if (_session.Role == SessionRole.Guest)
		{
			EnemyRosterBinder.Freeze(entity);
		}

		return false;
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

	/// <summary>Every animal this side knows about: the tracked set ∪ a scene scan (a just-instantiated copy whose Start has not run yet, or a set a roster reset cleared while the copies are still in the scene). Prunes the destroyed entries.</summary>
	private List<BuildingEntity> LocalAnimals() => EnemyAnimalScan.Union(_localAnimals);

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
		if (!WorldIsSettled())
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
		if (!WorldIsSettled())
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
