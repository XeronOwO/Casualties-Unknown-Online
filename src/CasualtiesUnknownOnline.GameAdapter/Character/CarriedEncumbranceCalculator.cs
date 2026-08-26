using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Computes the authoritative character-snapshot encumbrance that a carrier
/// should add to its own <c>Body.GetTotalEncumberance()</c> while carrying or
/// piggybacking a teammate. The remote rider is only a frozen render clone on
/// the carrier's side, so its in-game Item objects are not the rider's real
/// inventory; the 1 Hz character snapshot is the authoritative source for the
/// carried player's item weights.
/// </summary>
internal static class CarriedEncumbranceCalculator
{
	/// <summary>
	/// Full encumbrance of a character snapshot: every slot/worn item plus every
	/// recursively nested container content, using the same condition-scaling
	/// rule as <c>Item.totalWeight</c>.
	/// </summary>
	public static float ComputeFullEncumbrance(CharacterDataMsg data)
	{
		var total = 0f;
		foreach (var item in data.Items)
		{
			total += ComputeItemWeight(item);
		}

		return total;
	}

	public static float ApplyMultiplier(float fullEncumbrance, float multiplier) =>
		fullEncumbrance * Mathf.Max(0f, multiplier);

	private static float ComputeItemWeight(CharacterItemMsg item)
	{
		var weight = 0f;
		if (Item.GlobalItems.TryGetValue(item.ItemId, out var info))
		{
			weight = info.scaleWeightWithCondition
				? Mathf.Lerp(0.1f, info.weight, Mathf.Clamp01(item.Condition))
				: info.weight;
		}

		foreach (var child in item.Contents)
		{
			weight += ComputeItemWeight(child);
		}

		return weight;
	}
}
