using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.World;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// What a dying BUILDING ENTITY may drop — the third table a member's break
/// report is bounded against (<see cref="DropBoundPolicy"/>), and the one that
/// is neither code nor a mod declaration but the prefab's own Inspector data.
/// <para>
/// The table is read off the LIVE entity, which is what makes the host's capture
/// point the support-loss mark rather than the report: a building that loses its
/// support block is MARKED for a remote death by the side that did not roll, and
/// its own next <c>Update</c> destroys the object — by the time the break report
/// carrying the drops arrives (a frame later, or minutes later on a re-report)
/// there is no component left to read (<c>BuildingEntity.cs:74-120</c>).
/// </para>
/// <para>
/// The three buckets mirror the game's own roll one for one, and each bucket is
/// a different number of ROLLS (see <see cref="DropBound"/> for why that is the
/// unit):
/// </para>
/// <list type="bullet">
/// <item><c>itemsDropOnDestroy</c>: ONE chance-gated roll per entry
/// (<c>Random.Range(0f, 1f) &lt; entry.chance * dropChanceMultiplier</c>), so one
/// row per entry and the entry's own condition window. An entry the multiplier
/// cannot make positive is skipped — that gate is false for every roll, so the
/// row could never produce an item;</item>
/// <item><c>guaranteedDropAmount</c>: that many ungated rolls. Each one picks a
/// category from <c>itemCategoriesToAdd</c> and then an entry of that category's
/// <c>ItemLootPool</c> list, so ONE roll can name any id of ANY named category —
/// the row names the union, and the count is the number of rows, because two
/// rolls can produce two items of the same id. The condition is
/// <c>Random.Range(1f, 1f)</c>, exactly 1;</item>
/// <item><c>alwaysDrop</c>: one ungated roll per entry, the entry's own window.
/// Its <c>chance</c> field is deliberately not read — the game does not read it
/// here either.</item>
/// </list>
/// <para>
/// A category the pool does not know, a null pool and an empty category list
/// each contribute no ids: the game's own indexer would throw on the first, and
/// a roll that names nothing cannot create an item.
/// </para>
/// </summary>
internal static class GameBuildingDropTable
{
	private static readonly IReadOnlyList<string> NoIds = [];

	private static readonly IReadOnlyList<AuthoredDrop> NoEntries = [];

	/// <summary>
	/// The rows of ONE live building entity. Called while the entity still exists
	/// (its support-loss mark), never at judgement time.
	/// </summary>
	internal static IReadOnlyList<DropBound> ForEntity(BuildingEntity entity) =>
		RowsFor(
			Drops(entity.itemsDropOnDestroy),
			Drops(entity.alwaysDrop),
			entity.guaranteedDropAmount,
			entity.itemCategoriesToAdd,
			entity.dropChanceMultiplier,
			ItemLootPool.pool);

	/// <summary>
	/// The table's arithmetic over plain data, so it can be exercised without a
	/// live component: <paramref name="droppedOnDestroy"/> is the chance-gated
	/// bucket, <paramref name="alwaysDropped"/> the ungated one,
	/// <paramref name="categoriesToAdd"/> the pools the guaranteed rolls draw
	/// from, and <paramref name="dropChanceMultiplier"/> the entity's own
	/// multiplier over the first bucket's chances.
	/// </summary>
	internal static IReadOnlyList<DropBound> RowsFor(
		IReadOnlyList<AuthoredDrop> droppedOnDestroy,
		IReadOnlyList<AuthoredDrop> alwaysDropped,
		int guaranteedDropAmount,
		IReadOnlyList<string>? categoriesToAdd,
		float dropChanceMultiplier,
		IReadOnlyDictionary<string, List<string>>? lootPool)
	{
		var rows = new List<DropBound>(droppedOnDestroy.Count + alwaysDropped.Count + Math.Max(guaranteedDropAmount, 0));
		foreach (var entry in droppedOnDestroy)
		{
			// The gate the game asks before it creates this entry's item: a chance
			// the multiplier cannot lift above zero is false for every roll.
			if (string.IsNullOrWhiteSpace(entry.ItemId) || entry.Chance * dropChanceMultiplier <= 0f)
			{
				continue;
			}

			rows.Add(Row(entry));
		}

		foreach (var entry in alwaysDropped)
		{
			if (string.IsNullOrWhiteSpace(entry.ItemId))
			{
				continue;
			}

			rows.Add(Row(entry));
		}

		var poolIds = PoolIds(categoriesToAdd, lootPool);
		for (var roll = 0; roll < guaranteedDropAmount && poolIds.Count > 0; roll++)
		{
			// One row per guaranteed roll, each naming every id any named category
			// can offer, at the exactly-1 condition the game writes.
			rows.Add(new DropBound(poolIds, 1f, 1f));
		}

		return rows;
	}

	/// <summary>
	/// The condition window an entry's own roll produces. The game asks
	/// <c>Random.Range(conditionMin, conditionMax)</c>, whose float overload SWAPS
	/// its bounds when the minimum is the greater one — so the window is the
	/// authored pair in either order, and an authored row is never refused for
	/// having been written backwards.
	/// </summary>
	private static DropBound Row(AuthoredDrop entry) =>
		new([entry.ItemId], Mathf.Min(entry.MinCondition, entry.MaxCondition), Mathf.Max(entry.MinCondition, entry.MaxCondition));

	/// <summary>Every id the guaranteed rolls can reach: the union over the named categories of their pool lists.</summary>
	private static IReadOnlyList<string> PoolIds(IReadOnlyList<string>? categoriesToAdd, IReadOnlyDictionary<string, List<string>>? lootPool)
	{
		if (categoriesToAdd is not { Count: > 0 } || lootPool is null)
		{
			return NoIds;
		}

		var ids = new List<string>();
		foreach (var category in categoriesToAdd)
		{
			if (category is null || !lootPool.TryGetValue(category, out var pool) || pool is null)
			{
				continue;
			}

			foreach (var id in pool)
			{
				if (!string.IsNullOrWhiteSpace(id) && !ids.Contains(id))
				{
					ids.Add(id);
				}
			}
		}

		return ids.Count > 0 ? ids : NoIds;
	}

	/// <summary>The prefab's Inspector array as the table's own row shape. A null array is the empty set: the game's own <c>foreach</c> would throw on one, so a prefab carrying none is the only way to reach it.</summary>
	private static IReadOnlyList<AuthoredDrop> Drops(ItemDrop[]? entries)
	{
		if (entries is not { Length: > 0 })
		{
			return NoEntries;
		}

		var drops = new List<AuthoredDrop>(entries.Length);
		foreach (var entry in entries)
		{
			drops.Add(new AuthoredDrop(entry.id, entry.chance, entry.conditionMin, entry.conditionMax));
		}

		return drops;
	}
}
