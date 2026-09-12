using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// The container-location chain walk: the shared read-only traversal behind
/// "is this item inside that container", "how deep", "does the chain end in the
/// world", and the container-cycle check. Extracted from
/// <see cref="ItemDomainModule"/> (the 600-line architecture gate) because the
/// walk is one responsibility: it only follows parent links and never decides
/// anything — the domain module keeps every decision.
///
/// It is public because the rule it exposes has a second caller OUTSIDE this
/// project: the archive contract asks the same question when it decides which
/// item rows a layer-end cut carries (a world-rooted item describes the layer
/// being replaced). Two copies of this walk would eventually disagree about what
/// "in the world" means, and the layer-boundary reset and the archive would then
/// drop different subtrees.
/// </summary>
public static class ItemLocationChain
{
	/// <summary>
	/// Whether the item's location chain is rooted in the WORLD — the item lies
	/// on the ground, or inside a container that ultimately lies on the ground.
	/// A carried item (and its contents) is NOT world-rooted: it travels with the
	/// player. A terminal item is not world-rooted either — its chain has ended,
	/// and a terminal record is never resurrected or dropped.
	/// </summary>
	public static bool IsWorldRooted(ItemState item, Func<ulong, ItemState?> find)
	{
		if (item.Location.Kind == ItemLocationKind.World)
		{
			return true;
		}

		if (item.Location.Kind != ItemLocationKind.Contained)
		{
			return false;
		}

		var visited = new HashSet<ulong> { item.Identity.InstanceId };
		var cursor = item.Location.ParentItemId;
		while (cursor != 0 && visited.Add(cursor))
		{
			var parent = find(cursor);
			if (parent is null)
			{
				return false;
			}

			switch (parent.Value.Location.Kind)
			{
				case ItemLocationKind.World:
					return true;
				case ItemLocationKind.Contained:
					cursor = parent.Value.Location.ParentItemId;
					break;
				default:
					return false;
			}
		}

		return false;
	}

	/// <summary>Whether the item's parent chain reaches <paramref name="ancestorId"/>.</summary>
	public static bool IsDescendantOf(ulong itemId, ulong ancestorId, Func<ulong, ItemState?> find)
	{
		var current = find(itemId);
		if (current is null || current.Value.Location.Kind != ItemLocationKind.Contained)
		{
			return false;
		}

		var visited = new HashSet<ulong>();
		var cursor = current.Value.Location.ParentItemId;
		while (cursor != 0 && visited.Add(cursor))
		{
			if (cursor == ancestorId)
			{
				return true;
			}

			var parent = find(cursor);
			if (parent is null || parent.Value.Location.Kind != ItemLocationKind.Contained)
			{
				return false;
			}

			cursor = parent.Value.Location.ParentItemId;
		}

		return false;
	}

	/// <summary>
	/// How many container hops separate the item from <paramref name="ancestorId"/>
	/// (1 = a direct child); -1 when the chain does not reach it. The deepest item
	/// is destroyed first, so a nested container's contents never outlive it.
	/// </summary>
	public static int ContainedDepth(ulong itemId, ulong ancestorId, Func<ulong, ItemState?> find)
	{
		var current = find(itemId);
		if (current is null || current.Value.Location.Kind != ItemLocationKind.Contained)
		{
			return -1;
		}

		var depth = 0;
		var visited = new HashSet<ulong>();
		var cursor = current.Value.Location.ParentItemId;
		while (cursor != 0 && visited.Add(cursor))
		{
			depth++;
			if (cursor == ancestorId)
			{
				return depth;
			}

			var parent = find(cursor);
			if (parent is null || parent.Value.Location.Kind != ItemLocationKind.Contained)
			{
				return -1;
			}

			cursor = parent.Value.Location.ParentItemId;
		}

		return -1;
	}

	/// <summary>Reject a parent chain that loops back on itself (the kernel invariant).</summary>
	public static void AssertNoContainerCycles(IEnumerable<ItemState> items, Func<ulong, ItemState?> find)
	{
		foreach (var item in items)
		{
			if (item.Location.Kind != ItemLocationKind.Contained)
			{
				continue;
			}

			var visited = new HashSet<ulong> { item.Identity.InstanceId };
			var cursor = item.Location.ParentItemId;
			while (cursor != 0)
			{
				if (!visited.Add(cursor))
				{
					throw new InvalidOperationException($"container cycle detected at item {item.Identity.InstanceId} / parent {cursor}");
				}

				var parent = find(cursor);
				if (parent is null || parent.Value.Location.Kind != ItemLocationKind.Contained)
				{
					break;
				}

				cursor = parent.Value.Location.ParentItemId;
			}
		}
	}
}
