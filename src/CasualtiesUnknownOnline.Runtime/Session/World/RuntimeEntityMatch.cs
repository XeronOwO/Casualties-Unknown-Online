using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The runtime entity-creation match judgment (PURE — no Unity): which local
/// BuildingEntity a creation record binds to. Extracted from the adapter's
/// <c>EntitySpawnSync.FindExisting</c> so the identity contract is
/// unit-testable without a game scene, exactly like <c>TrapLayoutAlign</c> and
/// <c>EnemyRuntimeSpawnArbitration</c>.
/// <para>
/// ONE pass: the record's OWN copy — the candidate carrying the record's exact
/// <see cref="RuntimeEntityKey"/> (prefab id + creation cell + creation-instance
/// token), wherever it drifted (a BuildingEntity's Rigidbody2D becomes Dynamic
/// while its chunk is visible, BuildingEntity.cs:54). There is no positional
/// fallback any more: every copy that a record can legitimately bind carries
/// that key — the creating side stamps it at creation, the relay and the
/// snapshot stamp it on every receiver, the enemy-domain late-join backfill
/// stamps the key the host publishes with <c>EnemySnapshot.RuntimeSpawns</c>,
/// and the trap-layout materialization stamps the key the host scanned off its
/// own marked copy.
/// </para>
/// <para>
/// The deleted positional pass (a markerless same-prefab copy strictly inside
/// 1 m) was a false-bind source: a runtime creation landing next to an
/// unrelated markerless copy — a generated entity on the host, or an unrelated
/// host-authoritative replay on a guest — was absorbed into it, and the record
/// then stamped the WRONG entity with its key while the actual creation stayed
/// missing on that side (one-sided missing entity). A markerless copy is never
/// this record's copy, so it is never a bind target; a copy that carries a
/// DIFFERENT key is a sibling creation and is never a bind target either. The
/// tutorial-prop exclusion the positional pass needed is gone with it: a
/// per-player course prop carries no shared creation key, so it cannot be
/// bound.
/// </para>
/// </summary>
internal static class RuntimeEntityMatch
{
	/// <summary>One local candidate the adapter scanned: the creation marker it carries, or null for an entity that never entered the runtime-creation tables.</summary>
	internal readonly record struct Candidate(string Id, float X, float Y, RuntimeEntityKey? CreationKey);

	/// <summary>
	/// The index of the local copy this creation record binds to: the candidate
	/// carrying the same creation key, or -1 when none does.
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

		return -1;
	}
}
