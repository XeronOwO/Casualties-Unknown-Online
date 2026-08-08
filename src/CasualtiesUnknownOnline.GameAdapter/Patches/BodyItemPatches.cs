using System;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Inventory ownership hooks: every pickup (drag UI, auto-pickup, commands —
/// they all land in Body.PickUpItem), drop and wearable-drop. The Prefix
/// records the world position the item was picked up from (the rollback
/// target when the host refuses the pickup); the Postfix reports the change.
/// InventorySlot's unconscious drop (InventorySlot.cs:45-51) funnels through
/// DropItem(int) → DropItem(Item), so this one hook covers it.
/// </summary>
internal static class BodyItemPatches
{
	[HarmonyPatch(typeof(Body), "PickUpItem")]
	internal static class PickUpItemPatch
	{
		private static void Prefix(Item item) => PatchBridge.Impl?.OnItemPickupStart(item);

		private static void Postfix(Item item) => PatchBridge.Impl?.OnItemPickedUp(item);
	}

	[HarmonyPatch(typeof(Body), "DropItem", new Type[] { typeof(Item) })]
	internal static class DropItemPatch
	{
		private static void Postfix(Item item) => PatchBridge.Impl?.OnItemDropped(item);
	}

	[HarmonyPatch(typeof(Body), "DropWearable")]
	internal static class DropWearablePatch
	{
		private static void Postfix(Item item) => PatchBridge.Impl?.OnItemDropped(item);
	}
}
