using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The game's own container answer behind <c>/give</c>'s destination ladder: for each candidate the host's
/// snapshot carries, read the definition's <see cref="Container"/> component and apply the SAME rule the
/// receiving client's native <c>Container.CanHoldItem</c> applies when the item arrives —
/// <c>item.totalWeight &lt;= maxWeightPerItem</c>, <c>holdingWeight + item.totalWeight &lt;= maxWeight</c>,
/// and the tag restriction when the container declares one.
///
/// <para>
/// Everything read here is a definition fact: the prefab's serialized capacity and tag list, and the item
/// table's own weights. A prefab template has never run <c>Awake</c>, and <c>Container.Awake</c> adds 1 g to
/// both limits (<c>reversing/Assembly-CSharp/Assembly-CSharp/Container.cs</c>, lines 30-31), so the
/// comparison mirrors that adjustment rather than under-reporting a live container's room by one gram. The
/// weight is <c>Item.totalWeight</c>'s own formula: the definition weight, scaled by condition when the
/// definition says so (<c>Item.cs</c>, lines 13-23). A definition this build cannot read fails the whole
/// candidate — a container whose contents cannot be weighed is never guessed to have room. The candidates
/// are the body's TOP-LEVEL items (the Runtime passes them in ladder order); a container nested inside
/// another is never chosen, because the game's own <c>Container.LoadItem</c> refuses a destination that is
/// itself inside a container. The created item's own children are the other native refusal: an item that is
/// itself a container with holdings cannot go into another container.
/// </para>
/// </summary>
internal sealed class ItemContainerFit(ILogger<ItemContainerFit> log) : IItemContainerFit
{
	/// <summary><c>Container.Awake</c>'s own adjustment to the two limits; a prefab has not run it.</summary>
	private const float AwakeWeightEpsilon = 0.001f;

	private readonly ILogger<ItemContainerFit> _log = log;

	/// <inheritdoc />
	public bool TryFindContainer(IReadOnlyList<CharacterItemMsg> candidates, CharacterItemMsg item, out ulong containerId)
	{
		containerId = 0;
		if (item.Contents.Count > 0)
		{
			// Container.LoadItem refuses an item that is ITSELF a container with holdings ("alertcontainerstacking"),
			// so a fact carrying children can never be predicted into a container even though CanHoldItem would pass.
			return false;
		}

		if (!TryWeightOf(item, out var itemWeight) || !TryStats(item.ItemId, out var itemStats))
		{
			return false;
		}

		foreach (var candidate in candidates)
		{
			if (candidate.InstanceId == 0)
			{
				continue;
			}

			var prefab = ItemPrefabResolver.Load(candidate.ItemId);
			if (prefab == null) // Unity object — ==
			{
				continue;
			}

			var container = prefab.GetComponent<Container>();
			if (container == null) // Unity object — ==
			{
				continue;
			}

			var maxPerItem = container.maxWeightPerItem + AwakeWeightEpsilon;
			var maxTotal = container.maxWeight + AwakeWeightEpsilon;
			if (itemWeight > maxPerItem)
			{
				continue;
			}

			if (!TryHoldingWeight(candidate.Contents, out var holding) || holding + itemWeight > maxTotal)
			{
				continue;
			}

			if (!PassesTagRestriction(container, itemStats))
			{
				continue;
			}

			containerId = candidate.InstanceId;
			_log.LogInformation(
				"[Give] container {ContainerId} ({Definition}) can hold {ItemId} ({ItemWeight:F2} g): holding {Holding:F2}/{MaxTotal:F2} g, per item {MaxPerItem:F2} g.",
				candidate.InstanceId, candidate.ItemId, item.ItemId, itemWeight, holding, maxTotal, maxPerItem);
			return true;
		}

		return false;
	}

	/// <summary><c>Container.CanHoldItem</c>'s third clause: an empty restriction admits everything, otherwise at least one tag must match.</summary>
	private static bool PassesTagRestriction(Container container, ItemInfo stats)
	{
		var restriction = container.tagRestriction;
		if (restriction is not { Length: > 0 })
		{
			return true;
		}

		var itemTags = stats.GetTags();
		if (itemTags is not { Length: > 0 })
		{
			return false;
		}

		foreach (var tag in restriction)
		{
			foreach (var itemTag in itemTags)
			{
				if (string.Equals(tag, itemTag, StringComparison.Ordinal))
				{
					return true;
				}
			}
		}

		return false;
	}

	/// <summary>The holding weight <c>Container.GetHoldingWeight</c> sums: each DIRECT child's own total weight.</summary>
	private static bool TryHoldingWeight(List<CharacterItemMsg> contents, out float holding)
	{
		holding = 0f;
		foreach (var child in contents)
		{
			if (!TryWeightOf(child, out var weight))
			{
				return false;
			}

			holding += weight;
		}

		return true;
	}

	/// <summary><c>Item.totalWeight</c>: the definition's weight, scaled by condition when the definition says so — or false when this build does not name the definition.</summary>
	private static bool TryWeightOf(CharacterItemMsg item, out float weight)
	{
		if (!TryStats(item.ItemId, out var stats))
		{
			weight = 0f;
			return false;
		}

		weight = stats.scaleWeightWithCondition
			? Mathf.Lerp(0.1f, stats.weight, item.Condition)
			: stats.weight;
		return true;
	}

	/// <summary>The game's own item table; a definition it does not name is a candidate this build cannot judge.</summary>
	private static bool TryStats(string definitionId, out ItemInfo stats)
	{
		if (!string.IsNullOrWhiteSpace(definitionId) && Item.GlobalItems != null && Item.GlobalItems.TryGetValue(definitionId, out stats))
		{
			return true;
		}

		stats = null!;
		return false;
	}
}
