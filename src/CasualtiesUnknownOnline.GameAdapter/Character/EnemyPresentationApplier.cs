using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The guest's presentation-apply half of <see cref="EnemySyncCoordinator"/>:
/// it writes one buffered enemy state onto the copy bound to that id, and it
/// remembers the damage a local attack already dealt so the next host batch does
/// not flash the copy's health back up for a round-trip. It owns NO identity —
/// which copy is which id, the spawn anchor and the copy lifecycle stay in the
/// coordinator — which is what makes it the right home for the next
/// presentation field instead of the coordinator growing past the 600-line
/// architecture gate (extracted 2026-09-18: the coordinator sat exactly at the
/// cap before the enemy binding-recovery change landed in it).
/// </summary>
internal sealed class EnemyPresentationApplier(ILogger<EnemySyncCoordinator> log)
{
	private readonly ILogger<EnemySyncCoordinator> _log = log;

	private readonly Dictionary<NetworkEntityId, EnemyHealthReconcile> _healthReconcile = [];

	/// <summary>Apply the buffered state of every BOUND copy — an id with no local copy is a no-op.</summary>
	internal void ApplyAll(IEnumerable<EnemyEntity> enemies, Func<NetworkEntityId, BuildingEntity?> findEntity)
	{
		foreach (var state in enemies)
		{
			var entity = findEntity(state.EntityId);
			if (entity != null) // Unity object — ==
			{
				Apply(entity, state);
			}
		}
	}

	/// <summary>
	/// Write one authoritative state onto its local copy: the transform, the
	/// health — reconciled against the in-flight local damage — and the
	/// presentation side-effects (stun pose, spider leg IK targets, crystal
	/// wind-up telegraph).
	/// </summary>
	internal void Apply(BuildingEntity entity, EnemyEntity state)
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

		if (state.SpiderLegTargets is { Count: > 0 })
		{
			SpiderLegPresentation.Apply(entity, state.SpiderLegTargets);
		}

		if (CrystalWindupPresentation.Apply(entity, state.CrystalWindupAmount, state.CrystalLineEnd))
		{
			_log.LogInformation("[Enemy] {Enemy} crystal wind-up telegraph -> {Visible}.", state.EntityId, state.CrystalWindupAmount > 0f);
		}
	}

	/// <summary>
	/// A local attack damaged a frozen enemy copy (Body.Attack → the copy's
	/// health dropped before the report reaches the host): record the damage as
	/// pending so the next host batch does not revert it.
	/// </summary>
	internal void RecordLocalDamage(NetworkEntityId id, float damage)
	{
		if (!_healthReconcile.TryGetValue(id, out var reconcile))
		{
			reconcile = new EnemyHealthReconcile();
			_healthReconcile[id] = reconcile;
		}

		reconcile.RecordLocalDamage(damage);
	}

	/// <summary>The host removed this enemy — its pending local damage goes with it.</summary>
	internal void Forget(NetworkEntityId id) => _healthReconcile.Remove(id);

	/// <summary>The session ended — no pending local damage survives into the next one.</summary>
	internal void Clear() => _healthReconcile.Clear();
}
