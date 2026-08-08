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
	/// <summary>
	/// Guard while Body.SwapSlots re-parents items between slots (the drag UI):
	/// it internally drops and picks up both items, but nothing left the world —
	/// the reports would be false "placed"/"picked up" broadcasts.
	/// </summary>
	[HarmonyPatch(typeof(Body), "SwapSlots")]
	internal static class SwapSlotsPatch
	{
		internal static bool Swapping { get; private set; }

		private static void Prefix() => Swapping = true;

		private static void Postfix() => Swapping = false;
	}

	[HarmonyPatch(typeof(Body), "PickUpItem")]
	internal static class PickUpItemPatch
	{
		private static void Prefix(Item item) => PatchBridge.Impl?.OnItemPickupStart(item);

		// Only a pickup that actually landed (the guard clauses inside PickUpItem
		// — slot capacity, distance — can fail and leave the item untouched);
		// slot-to-slot moves (SwapSlots) are inventory-internal, not world events.
		private static void Postfix(Body __instance, Item item)
		{
			if (!SwapSlotsPatch.Swapping && __instance.HoldingItem(item))
			{
				PatchBridge.Impl?.OnItemPickedUp(item);
			}
		}
	}

	[HarmonyPatch(typeof(Body), "DropItem", new Type[] { typeof(Item) })]
	internal static class DropItemPatch
	{
		private static void Postfix(Item item)
		{
			if (!SwapSlotsPatch.Swapping)
			{
				PatchBridge.Impl?.OnItemDropped(item);
			}
		}
	}

	[HarmonyPatch(typeof(Body), "DropWearable")]
	internal static class DropWearablePatch
	{
		private static void Postfix(Item item) => PatchBridge.Impl?.OnItemDropped(item);
	}
}
