using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.World;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The host's native-to-DTO capture half of <see cref="EnemySyncCoordinator"/>:
/// it owns the bind-time spawn anchors and turns one live Unity enemy into the
/// <see cref="EnemyEntity"/> the state stream carries. The guest's write half is
/// <see cref="EnemyPresentationApplier"/>; the identity tables
/// (<c>_idByEntity</c>/<c>_entityById</c>) stay with the coordinator because the
/// guest's binding half shares them, which is why the host's id allocation
/// (<c>CaptureHostEnemies</c>/<c>EnsureMapping</c>/<c>Bind</c>) stayed there too.
/// Extracted 2026-10-02, before the runtime-spawn classification fix landed; the
/// architecture watchlist lists the coordinator near the 600-line gate.
/// </summary>
internal sealed class EnemyStateCapture(ILogger<EnemySyncCoordinator> log)
{
	private readonly ILogger<EnemySyncCoordinator> _log = log;

	/// <summary>The bind-time anchor each snapshot entry carries (the guest's pairing key): recorded once at the first bind, read by <see cref="Capture"/>, cleared with the session.</summary>
	private readonly Dictionary<BuildingEntity, NetVector2> _spawnAnchors = [];

	/// <summary>
	/// Record the anchor ONCE at the first bind — the mapping runs on the host's
	/// first capture after the enemy appeared, so this is its generation /
	/// creation position. That is the key the guest's frozen copies pair on:
	/// pairing on the live position only holds in the instant after generation,
	/// which is why a repair snapshot could never bind (N1).
	/// </summary>
	internal void RecordAnchor(BuildingEntity entity, NetVector2 anchor) => _spawnAnchors[entity] = anchor;

	internal void Clear() => _spawnAnchors.Clear();

	/// <summary>
	/// Turn one live host enemy into its presentation DTO: transform state,
	/// health/stun, the prefab id, the creation identity, and the presentation
	/// extras the receiving side cannot re-derive (mimic tint, spider leg
	/// targets, crystal wind-up telegraph).
	/// </summary>
	internal EnemyEntity Capture(BuildingEntity entity, NetworkEntityId id, bool runtimeSpawn)
	{
		var rb = entity.GetComponent<Rigidbody2D>();
		var spider = entity.GetComponentInChildren<SpiderHandler>();
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

		if (!_spawnAnchors.TryGetValue(entity, out var spawnPosition))
		{
			// The anchor is written before the first capture, so a miss means the
			// binding was bypassed. The anchor is load-bearing (a zero or moving
			// one silently breaks the repair pairing), so this degrades to the
			// live position LOUDLY instead of indexing blind.
			spawnPosition = new NetVector2(entity.transform.position.x, entity.transform.position.y);
			_log.LogWarning("[Enemy] {Enemy} captured without a bind-time spawn anchor — falling back to its live position; a repair snapshot may fail to pair it.", id);
		}

		return new EnemyEntity(id)
		{
			Position = new NetVector2(entity.transform.position.x, entity.transform.position.y),
			// The anchor travels beside the live position: the guest pairs on it,
			// the presentation keeps using Position.
			SpawnPosition = spawnPosition,
			Velocity = rb != null ? new NetVector2(rb.velocity.x, rb.velocity.y) : NetVector2.Zero,
			Rotation = entity.transform.eulerAngles.z,
			Health = entity.health,
			Stunned = EnemyStunPresentation.IsStunned(entity),
			PrefabId = entity.id,
			RuntimeSpawned = runtimeSpawn,
			// The host's own copy carries the creation identity when this animal
			// rode the entity-creation channel. Publishing it is what lets the
			// late-joiner backfill copy carry the SAME identity as the live copy
			// — a surviving live re-report then binds by key, never by the 1 m
			// positional fallback (which absorbed unrelated copies).
			CreationKey = RuntimeEntityCreation.TryRead(entity, out var creationKey) ? creationKey : null,
			HasTint = hasTint,
			TintColor = tint,
			TintLightIntensity = lightIntensity,
			SpiderLegTargets = SpiderLegPresentation.Capture(spider),
			CrystalWindupAmount = CrystalWindupPresentation.CaptureAmount(crystal),
			CrystalLineEnd = CrystalWindupPresentation.CaptureLineEnd(crystal),
		};
	}
}
