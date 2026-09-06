using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Pure decisions for the direct placeable-item use family. The game's
/// <c>ItemInfo.useAction</c> delegates for <c>scrapmetal</c>, <c>climbingrope</c>
/// and <c>scaffoldingpack</c> play <c>ArmsSwing</c> directly after a successful
/// placement (Item.cs:2165/2208/2249); the success signal is a condition
/// reduction written by the same action, and the same family filter opens the
/// placement-sound capture scope. This helper keeps the rules testable without
/// Unity and lets the Harmony patches remain thin adapters.
/// </summary>
internal static class DirectPlaceableArmSwingPolicy
{
	private static readonly HashSet<string> ItemIds =
		["scrapmetal", "climbingrope", "scaffoldingpack"];

	/// <summary>True when the item id belongs to the direct placeable family whose
	/// native use action opens the placement-sound capture scope.</summary>
	internal static bool IsPlaceable(string itemId) => ItemIds.Contains(itemId);

	/// <summary>True when a direct placeable use should open the placement-sound
	/// capture scope: the item is one of the family and the body is conscious
	/// (<c>Body.UseItemInHand</c> falls back to Attack when not conscious, so an
	/// unconscious swing must never be classified as an item placement).</summary>
	internal static bool ShouldOpenPlacementSoundScope(string itemId, bool conscious) =>
		conscious && IsPlaceable(itemId);

	/// <summary>
	/// True when the use was one of the direct placeable items and its condition
	/// actually dropped — i.e. the native action passed its gates and wrote the
	/// placement cost, so the ArmsSwing clip played.
	/// </summary>
	internal static bool ShouldReport(string itemId, float conditionBefore, float conditionAfter) =>
		ItemIds.Contains(itemId) && conditionBefore - conditionAfter > 0f;
}
