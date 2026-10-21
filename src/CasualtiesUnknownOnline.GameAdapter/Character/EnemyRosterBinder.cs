using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.World;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The guest's roster-adoption half: turns the host's enemy rows into local
/// copies. The judgment is the pure <see cref="EnemyRosterAdoption"/>; this class
/// owns the Unity effects — reuse an existing copy, materialize a missing row,
/// destroy a surplus copy — and the live pass that binds a row the 20 Hz stream
/// announced before the next snapshot. It owns no identity state of its own: the
/// tables come from <see cref="EnemyBindingTable"/> and the tracking sets from
/// the coordinator, so the host's capture and the guest's adoption can never
/// disagree about which local animal is already bound.
/// </summary>
internal sealed class EnemyRosterBinder(
	EnemySyncService enemies,
	EnemyBindingTable bindings,
	HashSet<BuildingEntity> runtimeAnimals,
	HashSet<BuildingEntity> localAnimals,
	EnemyPresentationApplier presentation,
	ILogger<EnemySyncCoordinator> log)
{
	private readonly EnemySyncService _enemies = enemies;
	private readonly EnemyBindingTable _bindings = bindings;
	private readonly HashSet<BuildingEntity> _runtimeAnimals = runtimeAnimals;
	private readonly HashSet<BuildingEntity> _localAnimals = localAnimals;
	private readonly EnemyPresentationApplier _presentation = presentation;
	private readonly ILogger<EnemySyncCoordinator> _log = log;

	/// <summary>
	/// Adopt the last applied roster (world entry, late join, the 60 s repair):
	/// drop bindings the roster no longer contains, then per row reuse, materialize
	/// or report; only a COMPLETE roster destroys the unclaimed local copies.
	/// </summary>
	internal void AdoptRoster()
	{
		PruneDeadBindings();

		var roster = _enemies.Enemies.ToList();
		var rosterIds = roster.Select(static state => state.EntityId).ToHashSet();
		var runtimeById = new Dictionary<NetworkEntityId, EnemySpawnEntryMsg>();
		foreach (var spawn in _enemies.RuntimeSpawns)
		{
			runtimeById[spawn.Id.ToNetworkEntityId()] = spawn;
		}

		var dropped = RemoveBoundEnemiesNotInRoster(rosterIds);
		PruneLocalAnimals();

		var unboundStates = roster.Where(state => !_bindings.Contains(state.EntityId)).ToList();
		var rows = new List<EnemyRosterAdoption.RosterRow>(unboundStates.Count);
		foreach (var state in unboundStates)
		{
			var key = runtimeById.TryGetValue(state.EntityId, out var spawn) && spawn.CreationKey is { } keyMsg
				? RuntimeEntityKey.FromKeyMsg(keyMsg)
				: (RuntimeEntityKey?)null;
			rows.Add(new EnemyRosterAdoption.RosterRow(state.EntityId, state.PrefabId, state.SpawnPosition, state.Position, key));
		}

		var candidates = new List<BuildingEntity>();
		var copies = new List<EnemyRosterAdoption.LocalCopy>(_localAnimals.Count);
		foreach (var entity in _localAnimals)
		{
			if (entity == null || _bindings.IsBound(entity)) // Unity object — ==
			{
				continue;
			}

			var position = entity.transform.position;
			var key = RuntimeEntityCreation.TryRead(entity, out var creationKey) ? creationKey : (RuntimeEntityKey?)null;
			copies.Add(new EnemyRosterAdoption.LocalCopy(
				candidates.Count,
				entity.id,
				new NetVector2(position.x, position.y),
				key));
			candidates.Add(entity);
		}

		var plan = EnemyRosterAdoption.Plan(rows, copies);

		var reused = 0;
		foreach (var pair in plan.Pairs)
		{
			var state = unboundStates[pair.RowIndex];
			var copy = candidates[pair.CopyIndex];
			_bindings.Bind(copy, state.EntityId);
			Freeze(copy);
			reused++;
			_log.LogDebug("[Enemy] roster row {Enemy} reused the local {Prefab} at ({X:F1},{Y:F1}) (hint distance {Distance:F3}).",
				state.EntityId, copy.id, copy.transform.position.x, copy.transform.position.y, pair.Distance);
		}

		var built = 0;
		var failed = 0;
		foreach (var rowIndex in plan.MaterializeRows)
		{
			var state = unboundStates[rowIndex];
			runtimeById.TryGetValue(state.EntityId, out var spawn);
			if (MaterializeRosterRow(state, spawn))
			{
				built++;
			}
			else
			{
				failed++;
			}
		}

		var destroyed = 0;
		var unresolvedCount = plan.Unresolved.Count + failed;
		if (unresolvedCount > 0)
		{
			// A roster that is not COMPLETE must not destroy anything: a row
			// without a prefab, and a row whose materialization FAILED, may each be
			// one of the unclaimed copies. Destroying there would remove the only
			// local copy of an enemy the pass could not otherwise bind. The next
			// snapshot (or the kernel fact's arrival) retries the whole pass.
			foreach (var unresolved in plan.Unresolved)
			{
				var state = unboundStates[unresolved.RowIndex];
				_log.LogWarning("[Enemy] roster row {Enemy} unresolved ({Reason}) — no prefab has reached this member yet; retried on the next snapshot, and {Count} surplus candidate(s) are held until the roster is complete.",
					state.EntityId, unresolved.Reason, plan.DestroyCopies.Count);
			}

			if (failed > 0)
			{
				_log.LogWarning("[Enemy] {Failed} roster row(s) could not be materialized — {Count} surplus candidate(s) are held until the next snapshot retries the roster.",
					failed, plan.DestroyCopies.Count);
			}
		}
		else
		{
			foreach (var copyIndex in plan.DestroyCopies)
			{
				DestroySurplus(candidates[copyIndex]);
				destroyed++;
			}
		}

		var aligned = unresolvedCount == 0 && roster.All(state => _bindings.Contains(state.EntityId));
		_log.LogInformation("[Enemy] roster adopted: {Rows} rows ({Kept} kept, {Reused} reused, {Built} built, {Destroyed} surplus destroyed, {Dropped} stale bindings dropped, {Unresolved} unresolved) — aligned={Aligned}.",
			roster.Count, roster.Count - unboundStates.Count, reused, built, destroyed, dropped, unresolvedCount, aligned);
	}

	/// <summary>A runtime-spawned enemy the 20 Hz stream announced before the next snapshot: the kernel fact names its prefab, the creation channel already created the local copy, and the two are close in time. Bind per id (nearest same-prefab copy inside the reuse radius); anything not bound here is materialized or reused by the next snapshot, which is the backfill of record.</summary>
	internal void OnStateReceived()
	{
		TryAdoptNewRows();
		ApplyAllStates();
	}

	/// <summary>Destroy the guest's local copies when the host reset the roster for a new layer — the objects themselves are gone with the scene, so only the bindings and tracking sets are dropped here.</summary>
	internal void ForgetAll()
	{
		_runtimeAnimals.Clear();
		_localAnimals.Clear();
	}

	/// <summary>Drop the binding of one enemy the host removed (or the adoption found stale). Returns the local object so the caller can destroy it.</summary>
	internal BuildingEntity? Unbind(NetworkEntityId id) => _bindings.Unbind(id);

	/// <summary>Remove the bindings whose Unity object no longer exists (a missed explicit removal, or a scene change).</summary>
	private void PruneDeadBindings()
	{
		foreach (var (id, entity) in _bindings.PruneDead())
		{
			_presentation.Forget(id);
			_runtimeAnimals.Remove(entity);
			_localAnimals.Remove(entity);
		}
	}

	/// <summary>A bound copy whose id is not in the roster is gone on the host (a removal the member missed) — the full snapshot is the removal's fallback, so it is dropped here rather than waiting for a repair that may never name it.</summary>
	private int RemoveBoundEnemiesNotInRoster(HashSet<NetworkEntityId> rosterIds)
	{
		var stale = _bindings.Ids.Where(id => !rosterIds.Contains(id)).ToList();
		foreach (var id in stale)
		{
			var entity = _bindings.Unbind(id);
			_presentation.Forget(id);
			if (entity != null) // Unity object — ==
			{
				_runtimeAnimals.Remove(entity);
				_localAnimals.Remove(entity);
				Object.Destroy(entity.gameObject);
				_log.LogInformation("[Enemy] guest destroyed local enemy {Enemy} — it is not in the host's roster (a missed removal).", id);
			}
		}

		return stale.Count;
	}

	/// <summary>Materialize one roster row the member has no copy for, at the host's CURRENT position — the stream corrects the position on the next batch, and the row's hint is only a reuse key.</summary>
	private bool MaterializeRosterRow(EnemyEntity state, EnemySpawnEntryMsg? spawn)
	{
		var position = new Vector2(state.Position.X, state.Position.Y);
		var created = RuntimeEntityFactory.TryCreate(state.PrefabId, position, _log, "EnemyRoster");
		if (created == null) // Unity object — ==
		{
			// The factory's line names the prefab and position; the row id is what a
			// post-mortem needs to pair the failure with the roster, and the failure
			// also holds the surplus destruction (the pass is incomplete).
			_log.LogWarning("[Enemy] roster row {Enemy} (prefab {Prefab}) could not be materialized at ({X:F1},{Y:F1}) — the row is retried on the next snapshot and its surplus candidates are held.",
				state.EntityId, state.PrefabId, position.x, position.y);
			return false;
		}

		created.transform.eulerAngles = new Vector3(0f, 0f, state.Rotation);
		created.gameObject.AddComponent<SpawnReplayMarker>(); // its own Start must not re-report the materialization
		if (spawn?.CreationKey is { } keyMsg)
		{
			// A runtime row the member never created carries the host's creation
			// identity, so a surviving live report can bind THIS copy by key.
			RuntimeEntityCreation.Stamp(created, RuntimeEntityKey.FromKeyMsg(keyMsg));
		}

		_bindings.Bind(created, state.EntityId);
		ApplySpawnTint(created, spawn);
		Freeze(created);
		_localAnimals.Add(created);
		_log.LogInformation("[Enemy] materialized roster enemy {Enemy} (prefab {Prefab}) at ({X:F1},{Y:F1}).",
			state.EntityId, state.PrefabId, position.x, position.y);
		return true;
	}

	/// <summary>Destroy one local animal the host's roster does not contain. The full snapshot is authoritative, so a member never keeps a local-only enemy (it could not be damaged authoritatively and would drop local items).</summary>
	private void DestroySurplus(BuildingEntity entity)
	{
		var position = entity.transform.position;
		_localAnimals.Remove(entity);
		_runtimeAnimals.Remove(entity);
		_log.LogInformation("[Enemy] destroyed surplus local enemy (prefab {Prefab}) at ({X:F1},{Y:F1}) — it is not in the host's roster.",
			entity.id, position.x, position.y);
		Object.Destroy(entity.gameObject);
	}

	private void TryAdoptNewRows()
	{
		var unbound = _enemies.Enemies.Where(state => !_bindings.Contains(state.EntityId)).ToList();
		if (unbound.Count == 0)
		{
			return;
		}

		PruneLocalAnimals();
		var candidates = _localAnimals
			.Where(entity => entity != null && !_bindings.IsBound(entity)) // Unity object — ==
			.ToList();
		if (candidates.Count == 0)
		{
			return;
		}

		// The creation key the last snapshot carried is the only exact identity a
		// live row can have; a row announced after that snapshot has none yet and
		// binds the nearest copy provisionally. The snapshot's plan owns the final
		// mapping, so a provisional bind can never be moved onto another row.
		var keyByRow = new Dictionary<NetworkEntityId, RuntimeEntityKey>();
		foreach (var spawn in _enemies.RuntimeSpawns)
		{
			if (spawn.CreationKey is { } keyMsg)
			{
				keyByRow[spawn.Id.ToNetworkEntityId()] = RuntimeEntityKey.FromKeyMsg(keyMsg);
			}
		}

		var copies = new List<EnemyRosterAdoption.LocalCopy>(candidates.Count);
		for (var i = 0; i < candidates.Count; i++)
		{
			var position = candidates[i].transform.position;
			var key = RuntimeEntityCreation.TryRead(candidates[i], out var creationKey) ? creationKey : (RuntimeEntityKey?)null;
			copies.Add(new EnemyRosterAdoption.LocalCopy(i, candidates[i].id, new NetVector2(position.x, position.y), key));
		}

		var used = new bool[candidates.Count];
		var adopted = 0;
		foreach (var state in unbound)
		{
			// Prefab AND runtime marker come from the SAME kernel fact: the 20 Hz
			// frame carries presentation only, so the buffer row of an id no snapshot
			// has introduced yet reads RuntimeSpawned=false — reading it here made
			// FindLiveCopy refuse the creation-channel copy the member had just
			// created, and the row waited for the next snapshot (batch 20261021-e,
			// row 4). The fact's absence is the retry case, not a judgment.
			if (!_enemies.TryGetEnemyFacts(state.EntityId, out var prefab, out var runtimeSpawned) || string.IsNullOrEmpty(prefab))
			{
				continue; // the kernel fact has not reached this member yet — the next batch retries
			}

			var key = keyByRow.TryGetValue(state.EntityId, out var knownKey) ? knownKey : (RuntimeEntityKey?)null;
			var bestIndex = EnemyRosterAdoption.FindLiveCopy(state.Position, prefab, key, runtimeSpawned, copies, used);
			if (bestIndex < 0)
			{
				continue;
			}

			used[bestIndex] = true;
			_bindings.Bind(candidates[bestIndex], state.EntityId);
			Freeze(candidates[bestIndex]);
			adopted++;
			_log.LogDebug("[Enemy] live roster row {Enemy} bound the local {Prefab} at ({X:F1},{Y:F1}).",
				state.EntityId, prefab, copies[bestIndex].Position.X, copies[bestIndex].Position.Y);
		}

		if (adopted > 0)
		{
			_log.LogInformation("[Enemy] live-adopted {Count} new enemy row(s) from the stream.", adopted);
		}
	}

	private void PruneLocalAnimals() => _localAnimals.RemoveWhere(entity => entity == null); // Unity object — ==

	private void ApplyAllStates() => _presentation.ApplyAll(_enemies.Enemies, _bindings.Find);

	/// <summary>Apply the spawned crystalenemy's carried presentation tint to the copy — the exact host-captured post-SetColor color and light intensity (CrystalEnemy.cs:208-216), written directly (never the native SetColor — its per-side-random jitter would diverge). A late joiner never saw the trigger side's SetColor, so the backfill entry must paint its copy.</summary>
	private void ApplySpawnTint(BuildingEntity entity, EnemySpawnEntryMsg? spawn)
	{
		if (spawn is not { HasTint: true })
		{
			return;
		}

		var crystal = entity.GetComponentInChildren<CrystalEnemy>();
		if (crystal == null) // Unity object — == (a non-crystalenemy prefab despite the flag — nothing to paint)
		{
			return;
		}

		var tint = spawn.TintColor.ToNetColorRgba();
		CrystalEnemyTintAccess.ApplyTint(crystal, new Color(tint.R, tint.G, tint.B, tint.A), spawn.LightIntensity);
	}

	internal static void Freeze(BuildingEntity entity)
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
}
