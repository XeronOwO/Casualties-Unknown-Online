using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// The enemy roster adoption judgment (PURE — no Unity; the host's rows and the
/// member's local copies are explicit inputs). The host's enemy table is the
/// roster: every enemy has a host id, so the member never has to reproduce the
/// host's generation to know which local animal is which. A row is paired with a
/// local copy only to REUSE a copy that is plausibly the same animal — a
/// creation key when both carry one, otherwise the nearest same-prefab copy
/// inside <see cref="MatchRadius"/> of the row's match hint. Everything
/// unmatched is stated rather than refused: an unmatched row is materialized by
/// the caller, an unclaimed copy is destroyed, and a row without a prefab is
/// unresolved (the caller reports it and retries).
/// <para>
/// This replaces the rejected position-as-identity scheme. The position is only
/// a reuse hint: a copy outside the radius is not a divergence that fails a
/// set, it is simply a copy the caller replaces with one at the host's row. A
/// keyed row never falls back to a keyless copy positionally, so an animal the
/// host attributed to a creation record can never be duplicated onto an
/// unrelated copy.
/// </para>
/// </summary>
internal static class EnemyRosterAdoption
{
	/// <summary>Max distance between a row's match hint and a local copy for the copy to be REUSED (world units). Only a reuse heuristic: outside it the caller materializes the row, so the number decides reuse only — a failed materialization is the caller's incomplete-roster case, not this radius.</summary>
	internal const float MatchRadius = 3f;

	/// <summary>
	/// The ONE runtime-spawn rule, applied by BOTH roles at the animal's own
	/// <c>BuildingEntity.Start</c>: an animal is a runtime spawn only when the
	/// session was up AND it appeared AFTER this layer's generation-finished edge.
	/// The edge is the SET of animals that existed when the world settled (the
	/// caller records it at its first <see cref="CanPublishRoster"/> frame), never
	/// the <c>generatingWorld</c> flag read at <c>Start</c>: a dense world's
	/// generator leaves hundreds of <c>Start</c> calls to run over the seconds
	/// after that flag flips, and every one of them read as a runtime spawn (batch
	/// `20261021-f`: 291 → 873 runtime spawns on a 956-animal layer whose rows were
	/// all generation output). Before the edge has been observed on a side, nothing
	/// is a runtime spawn — every animal present then belongs to the generation.
	/// </summary>
	internal static bool IsRuntimeSpawn(bool sessionActive, bool generationEdgeObserved, bool existedAtGenerationEdge) =>
		sessionActive && generationEdgeObserved && !existedAtGenerationEdge;

	/// <summary>
	/// When the world is SETTLED: it exists, is not generating, and is not in the
	/// regeneration fade the game runs before <c>generatingWorld</c> turns true.
	/// The host may publish an authoritative roster only then (a partial or empty
	/// table mid-transition would tell a member to delete the enemies its own
	/// generation has not produced yet), and the member may adopt only then (the
	/// live pass would otherwise bind copies of the scene being replaced).
	/// </summary>
	internal static bool CanPublishRoster(bool hasWorld, bool generating, bool regenerating) =>
		hasWorld && !generating && !regenerating;

	/// <summary>One row of the host's enemy table as a member sees it: identity, the prefab to materialize, the host's match hint (where it first bound the enemy — a hint, never an identity), the host's current position (where a missing copy is materialized) and the runtime creation key when the row rode the creation channel.</summary>
	internal readonly record struct RosterRow(
		NetworkEntityId Id,
		string PrefabId,
		NetVector2 MatchHint,
		NetVector2 Position,
		RuntimeEntityKey? CreationKey);

	/// <summary>One unbound local animal the member could reuse: the caller's own index, its prefab, its frozen position and the creation key it carries (if any).</summary>
	internal readonly record struct LocalCopy(
		int Index,
		string PrefabId,
		NetVector2 Position,
		RuntimeEntityKey? CreationKey);

	internal readonly record struct Pair(int RowIndex, int CopyIndex, float Distance);

	internal enum UnresolvedReason
	{
		/// <summary>The row has no prefab (its kernel fact has not reached this member yet) — the caller reports it and retries on the next pass.</summary>
		MissingPrefab,
	}

	internal readonly record struct UnresolvedRow(int RowIndex, UnresolvedReason Reason);

	/// <summary>
	/// What one pass must do. <see cref="DestroyCopies"/> is only surplus against a
	/// COMPLETE roster: when <see cref="Unresolved"/> is non-empty the caller must
	/// not destroy anything, because a row it cannot identify yet may be one of
	/// the unclaimed copies. <see cref="MaterializeRows"/> are the rows with no
	/// local copy to reuse, in the caller's row indices.
	/// </summary>
	internal sealed record AdoptionPlan(
		IReadOnlyList<Pair> Pairs,
		IReadOnlyList<int> MaterializeRows,
		IReadOnlyList<int> DestroyCopies,
		IReadOnlyList<UnresolvedRow> Unresolved);

	internal static AdoptionPlan Plan(IReadOnlyList<RosterRow> rows, IReadOnlyList<LocalCopy> copies)
	{
		var pairs = new List<Pair>();
		var materialize = new List<int>();
		var unresolved = new List<UnresolvedRow>();
		var claimed = new bool[copies.Count];

		// 1. Creation-key pass: a row the host attributed to a creation record binds
		// the copy carrying that same key, distance-free. A keyed row with no keyed
		// copy is materialized — never positionally absorbed by a keyless copy.
		for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
		{
			var row = rows[rowIndex];
			if (string.IsNullOrEmpty(row.PrefabId))
			{
				unresolved.Add(new UnresolvedRow(rowIndex, UnresolvedReason.MissingPrefab));
				continue;
			}

			if (row.CreationKey is not { } key)
			{
				continue;
			}

			var bound = false;
			for (var copyIndex = 0; copyIndex < copies.Count; copyIndex++)
			{
				if (claimed[copyIndex] || copies[copyIndex].CreationKey != key)
				{
					continue;
				}

				claimed[copyIndex] = true;
				pairs.Add(new Pair(rowIndex, copyIndex, 0f));
				bound = true;
				break;
			}

			if (!bound)
			{
				materialize.Add(rowIndex);
			}
		}

		// 2. Positional pass: keyless rows claim the nearest unclaimed keyless copy
		// of the same prefab inside the reuse radius. Same-prefab copies are
		// interchangeable (the state stream drives the bound copy to the host's
		// position), so a nearest choice can only cost a one-frame pop, never a
		// wrong identity. The order is deterministic and independent of the input
		// order, so two members on the same inputs adopt the same copies.
		var positionalRows = rows
			.Select((row, index) => (Row: row, Index: index))
			.Where(r => r.Row.CreationKey is null && !string.IsNullOrEmpty(r.Row.PrefabId))
			.OrderBy(r => r.Row.PrefabId, StringComparer.Ordinal)
			.ThenBy(r => r.Row.MatchHint.X)
			.ThenBy(r => r.Row.MatchHint.Y)
			.ThenBy(r => r.Row.Id.Epoch)
			.ThenBy(r => r.Row.Id.Counter)
			.ThenBy(r => r.Index)
			.ToList();
		var positionalCopies = Enumerable.Range(0, copies.Count)
			.Where(copyIndex => copies[copyIndex].CreationKey is null)
			.OrderBy(copyIndex => copies[copyIndex].Position.X)
			.ThenBy(copyIndex => copies[copyIndex].Position.Y)
			.ThenBy(copyIndex => copyIndex)
			.ToList();
		foreach (var (row, rowIndex) in positionalRows)
		{
			var bestIndex = -1;
			var bestDistance = float.MaxValue;
			foreach (var copyIndex in positionalCopies)
			{
				if (claimed[copyIndex])
				{
					continue;
				}

				var copy = copies[copyIndex];
				if (!string.Equals(copy.PrefabId, row.PrefabId, StringComparison.Ordinal))
				{
					continue;
				}

				var distance = Distance(row.MatchHint, copy.Position);
				if (distance > MatchRadius || distance >= bestDistance)
				{
					continue;
				}

				bestDistance = distance;
				bestIndex = copyIndex;
			}

			if (bestIndex < 0)
			{
				materialize.Add(rowIndex);
				continue;
			}

			claimed[bestIndex] = true;
			pairs.Add(new Pair(rowIndex, bestIndex, bestDistance));
		}

		var destroy = new List<int>();
		for (var copyIndex = 0; copyIndex < copies.Count; copyIndex++)
		{
			if (!claimed[copyIndex])
			{
				destroy.Add(copyIndex);
			}
		}

		return new AdoptionPlan(pairs, materialize, destroy, unresolved);
	}

	/// <summary>
	/// Choose the local copy a LIVE-announced row can bind right now (the 20 Hz
	/// pass, before the next snapshot). When the row's creation key is known (the
	/// last snapshot carried it), only the copy with that exact key can match —
	/// identity wins over distance, so a keyed copy is never handed to another
	/// same-prefab row. A runtime row whose key is not known yet may bind a keyed
	/// copy provisionally (the creation channel just made it); a generated row
	/// only binds an unkeyed copy, the same discipline as <see cref="Plan"/>.
	/// Returns -1 when no copy may bind; the next batch retries, and the next
	/// snapshot's plan owns the final mapping.
	/// </summary>
	internal static int FindLiveCopy(
		NetVector2 position,
		string prefabId,
		RuntimeEntityKey? creationKey,
		bool allowKeyedCopy,
		IReadOnlyList<LocalCopy> copies,
		IReadOnlyList<bool> claimed)
	{
		if (creationKey is { } key)
		{
			for (var i = 0; i < copies.Count; i++)
			{
				if (!claimed[i] && copies[i].CreationKey == key)
				{
					return i;
				}
			}

			return -1;
		}

		var best = -1;
		var bestDistance = float.MaxValue;
		for (var i = 0; i < copies.Count; i++)
		{
			var copy = copies[i];
			if (claimed[i]
				|| (copy.CreationKey is not null && !allowKeyedCopy)
				|| !string.Equals(copy.PrefabId, prefabId, StringComparison.Ordinal))
			{
				continue;
			}

			var distance = Distance(position, copy.Position);
			if (distance > MatchRadius || distance >= bestDistance)
			{
				continue;
			}

			bestDistance = distance;
			best = i;
		}

		return best;
	}

	internal static float Distance(NetVector2 a, NetVector2 b)
	{
		var dx = a.X - b.X;
		var dy = a.Y - b.Y;
		return (float)Math.Sqrt((dx * dx) + (dy * dy));
	}
}
