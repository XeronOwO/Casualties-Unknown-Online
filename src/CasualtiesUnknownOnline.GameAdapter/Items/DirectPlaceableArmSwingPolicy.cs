using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Pure decision for the direct placeable-item arm-swing report. The game's
/// <c>ItemInfo.useAction</c> delegates for <c>scrapmetal</c>, <c>climbingrope</c>
/// and <c>scaffoldingpack</c> play <c>ArmsSwing</c> directly after a successful
/// placement (Item.cs:2165/2208/2249); the success signal is a condition
/// reduction written by the same action. This helper keeps the success rule
/// testable without Unity and lets the Harmony patch remain a thin adapter.
/// </summary>
internal static class DirectPlaceableArmSwingPolicy
{
	private static readonly HashSet<string> ItemIds =
		["scrapmetal", "climbingrope", "scaffoldingpack"];

	/// <summary>
	/// True when the use was one of the direct placeable items and its condition
	/// actually dropped — i.e. the native action passed its gates and wrote the
	/// placement cost, so the ArmsSwing clip played.
	/// </summary>
	internal static bool ShouldReport(string itemId, float conditionBefore, float conditionAfter) =>
		ItemIds.Contains(itemId) && conditionBefore - conditionAfter > 0f;
}
