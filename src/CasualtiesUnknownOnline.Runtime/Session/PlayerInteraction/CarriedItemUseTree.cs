using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The recursive carried-item tree the cross-player item-use families read and
/// mutate, plus the verdict that decides whether an item is a candidate for the
/// one-shot path at all. Split out of
/// <see cref="PlayerItemUseService"/> at the architecture line gate: the service
/// owns the family chain, the host's commit and the result, while everything here
/// is a pure function of an item (sub)tree and the game-data seam.
/// <para>
/// The lookup and the two mutations are recursive so container-nested items
/// (trash bags, backpacks) behave exactly like direct slots — the same reach the
/// item domain has.
/// </para>
/// </summary>
internal static class CarriedItemUseTree
{
	/// <summary>
	/// The first carried item the one-shot path would auto-select, in tree order:
	/// an item in a real slot whose own data puts it in one of the families the
	/// host computes for itself.
	/// </summary>
	internal static CharacterItemMsg? FindFirstUsable(
		IReadOnlyList<CharacterItemMsg> items,
		ILimbUseSemantics limbSemantics,
		IConsumeSemantics consumeSemantics)
	{
		foreach (var candidate in items)
		{
			if (candidate.SlotIndex >= 0
				&& candidate.InstanceId != 0
				&& IsActuallyUsable(candidate, limbSemantics, consumeSemantics))
			{
				return candidate;
			}

			var nested = FindFirstUsable(candidate.Contents, limbSemantics, consumeSemantics);
			if (nested is not null)
			{
				return nested;
			}
		}

		return null;
	}

	/// <summary>True when the item's own data still puts it in a family this path carries (and, for the condition-costing families, it has condition left).</summary>
	internal static bool IsActuallyUsable(
		CharacterItemMsg item,
		ILimbUseSemantics limbSemantics,
		IConsumeSemantics consumeSemantics)
	{
		if (item.Condition <= 0f
			&& (RemoteConsumeCatalog.IsFoodItem(item.ItemId)
				|| RemoteLimbToolCatalog.IsToolItem(item.ItemId)
				|| RemoteWearCatalog.IsWearItem(item.ItemId)))
		{
			return false;
		}

		return RemoteWearCatalog.IsWearItem(item.ItemId)
			|| RemoteConsumeCatalog.IsFoodItem(item.ItemId)
			|| ConsumeAdmission.IsDrinkContainer(consumeSemantics, item.ItemId, item.Liquids)
			|| TopicalAdmission.IsTopicalContainer(limbSemantics, item.ItemId, item.Liquids)
			|| RemoteLimbToolCatalog.IsToolItem(item.ItemId);
	}

	/// <summary>
	/// Find one carried item by instance id, recursing through container contents.
	/// Worn items (negative slot) are not selectable through the one-shot path;
	/// container contents carry the parent's non-negative slot, so recursion still
	/// covers nested items.
	/// </summary>
	internal static bool TryFind(IReadOnlyList<CharacterItemMsg> items, ulong instanceId, out CharacterItemMsg item)
	{
		foreach (var candidate in items)
		{
			if (candidate.SlotIndex < 0 || candidate.InstanceId == 0)
			{
				continue;
			}

			if (instanceId != 0 && candidate.InstanceId == instanceId)
			{
				item = candidate;
				return true;
			}

			if (TryFind(candidate.Contents, instanceId, out item))
			{
				return true;
			}
		}

		item = null!;
		return false;
	}

	internal static bool Remove(List<CharacterItemMsg> items, ulong instanceId)
	{
		for (var i = 0; i < items.Count; i++)
		{
			if (items[i].InstanceId == instanceId)
			{
				items.RemoveAt(i);
				return true;
			}

			if (Remove(items[i].Contents, instanceId))
			{
				return true;
			}
		}

		return false;
	}

	internal static bool Replace(List<CharacterItemMsg> items, ulong instanceId, CharacterItemMsg replacement)
	{
		for (var i = 0; i < items.Count; i++)
		{
			if (items[i].InstanceId == instanceId)
			{
				items[i] = replacement;
				return true;
			}

			if (Replace(items[i].Contents, instanceId, replacement))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Debit one committed drain from the item's own liquid stacks, index-wise, and
	/// reconstruct its condition.
	/// <para>
	/// The wire item has no Capacity field; condition is total/capacity for a
	/// <c>WaterContainerItem</c>. Reconstructing it from the original total/condition
	/// ratio lets the local item update and the host record agree without needing
	/// the game's <c>LiquidItemInfo</c> table.
	/// </para>
	/// </summary>
	internal static void ApplyDrain(CharacterItemMsg item, IReadOnlyList<LiquidStackMsg> plan)
	{
		var originalTotal = item.Liquids.Sum(s => s.Amount);
		var after = new List<LiquidStackMsg>(item.Liquids.Count);
		for (var i = 0; i < item.Liquids.Count; i++)
		{
			var consumed = i < plan.Count ? plan[i].Amount : 0f;
			after.Add(new LiquidStackMsg
			{
				LiquidId = item.Liquids[i].LiquidId,
				Amount = Math.Max(0f, item.Liquids[i].Amount - consumed),
			});
		}

		after.RemoveAll(s => s.Amount < 0.5f);
		var afterTotal = after.Sum(s => s.Amount);
		item.Liquids = after;

		item.Condition = originalTotal > 0f && item.Condition > 0f
			? afterTotal * item.Condition / originalTotal
			: 0f;
	}
}
