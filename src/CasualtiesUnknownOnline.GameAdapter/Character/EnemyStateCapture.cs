using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.World;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The host's native-to-DTO capture half of <see cref="EnemySyncCoordinator"/>:
/// it owns the per-enemy MATCH HINTS (a reuse hint for the guest's roster
/// adoption, never an identity) and turns one live Unity enemy into the
/// <see cref="EnemyEntity"/> the state stream carries. The guest's write half is
/// <see cref="EnemyPresentationApplier"/>; the identity tables are
/// <see cref="EnemyBindingTable"/>, shared by the host capture and the guest's
/// adoption, which is why the host's id allocation
/// (<c>CaptureHostEnemies</c>/<c>EnsureMapping</c>/<c>BindHost</c>) stayed in the
/// coordinator too.
/// </summary>
internal sealed class EnemyStateCapture(ILogger<EnemySyncCoordinator> log)
{
	private readonly ILogger<EnemySyncCoordinator> _log = log;

	/// <summary>The match hint each snapshot entry carries: recorded once at the first bind, read by <see cref="Capture"/>, cleared with the session and pruned when the entity dies.</summary>
	private readonly Dictionary<BuildingEntity, NetVector2> _matchHints = [];

	/// <summary>
	/// Record the MATCH HINT once at the first bind — where the host first saw the
	/// animal. It is a reuse hint for the guest's adoption, never an identity: a
	/// row the hint cannot place is materialized at the host's current position, so
	/// a hint recorded after the animal moved cannot leave the enemy unbound.
	/// </summary>
	internal void RecordMatchHint(BuildingEntity entity, NetVector2 hint) => _matchHints[entity] = hint;

	/// <summary>Drop one dead entity's hint (the binding is being removed; the dictionary must not keep a destroyed key).</summary>
	internal void Forget(BuildingEntity entity) => _matchHints.Remove(entity);

	internal void Clear() => _matchHints.Clear();

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

		if (!_matchHints.TryGetValue(entity, out var matchHint))
		{
			// The hint is written before the first capture, so a miss means the
			// binding was bypassed. It is only a reuse hint now, but a zero hint
			// would still make every adoption replace this animal instead of
			// reusing it, so this degrades to the live position LOUDLY.
			matchHint = new NetVector2(entity.transform.position.x, entity.transform.position.y);
			_log.LogWarning("[Enemy] {Enemy} captured without a match hint — falling back to its live position; the member may replace rather than reuse this copy.", id);
		}

		return new EnemyEntity(id)
		{
			Position = new NetVector2(entity.transform.position.x, entity.transform.position.y),
			// The hint travels beside the live position: the guest uses it only to
			// pick a local copy to reuse, the presentation keeps using Position.
			SpawnPosition = matchHint,
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
