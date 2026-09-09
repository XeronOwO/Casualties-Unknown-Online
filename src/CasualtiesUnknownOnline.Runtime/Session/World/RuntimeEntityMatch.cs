using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The runtime entity-creation match judgment (PURE — no Unity): which local
/// BuildingEntity a creation record binds to. Extracted from the adapter's
/// <c>EntitySpawnSync.FindExisting</c> so the identity contract is
/// unit-testable without a game scene, exactly like <c>TrapLayoutAlign</c> and
/// <c>EnemyRuntimeSpawnArbitration</c>.
/// <para>
/// Two passes, in this order:
/// <list type="number">
/// <item>The record's OWN copy: the candidate carrying the record's exact
/// <see cref="RuntimeEntityKey"/> (prefab id + creation cell + creation-instance
/// token), wherever it drifted (a BuildingEntity's Rigidbody2D becomes Dynamic
/// while its chunk is visible, BuildingEntity.cs:54).</item>
/// <item>A MARKERLESS copy strictly inside <see cref="MatchRadius"/> of the
/// recorded position: an enemy-domain backfill copy
/// (<c>EnemySyncCoordinator.CreateRuntimeSpawn</c> materializes from the host's
/// enemy snapshot at the animal's CURRENT position) or a generated entity.
/// These never entered the runtime-creation tables and can only be recognised by
/// position. (A trap-layout materialization does NOT belong here any more:
/// <c>TrapLayoutApplication.Materialize</c> stamps a <c>SpawnReplayMarker</c>,
/// because its <c>Start</c> runs after the <c>RemoteApply</c> scope closed and
/// would otherwise report a host-authoritative layout replay as a runtime
/// creation.)</item>
/// </list>
/// The radius pass deliberately skips every candidate that CARRIES a marker: a
/// sibling creation's copy holds a different key, and binding this record to it
/// swallowed the second entity (round-3 finding 1 — two identical prefabs
/// created inside one cell). The tutorial-prop exclusion keeps a shared-domain
/// record from binding a per-player course object.
/// </para>
/// </summary>
internal static class RuntimeEntityMatch
{
	/// <summary>The markerless-copy match radius (metres) — see the class remarks.</summary>
	internal const float MatchRadius = 1f;

	/// <summary>One local candidate the adapter scanned: the creation marker it carries, or null for an entity that never entered the runtime-creation tables.</summary>
	internal readonly record struct Candidate(string Id, float X, float Y, bool IsTutorialProp, RuntimeEntityKey? CreationKey);

	/// <summary>
	/// The index of the local copy this creation record binds to: first the
	/// candidate carrying the same creation key, then a markerless same-prefab,
	/// non-tutorial candidate strictly inside <see cref="MatchRadius"/> of the
	/// recorded position. -1 when none binds.
	/// </summary>
	internal static int FindIndex(IReadOnlyList<Candidate> candidates, RuntimeEntityKey key, float x, float y)
	{
		for (var i = 0; i < candidates.Count; i++)
		{
			if (candidates[i].CreationKey is { } candidateKey && candidateKey == key)
			{
				return i;
			}
		}

		var radiusSquared = MatchRadius * MatchRadius;
		for (var i = 0; i < candidates.Count; i++)
		{
			var candidate = candidates[i];
			if (candidate.CreationKey is not null || candidate.IsTutorialProp
				|| !string.Equals(candidate.Id, key.Id, StringComparison.Ordinal))
			{
				continue;
			}

			var dx = candidate.X - x;
			var dy = candidate.Y - y;
			if ((dx * dx) + (dy * dy) < radiusSquared)
			{
				return i;
			}
		}

		return -1;
	}
}
