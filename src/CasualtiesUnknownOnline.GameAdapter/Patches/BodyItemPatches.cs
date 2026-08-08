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

	/// <summary>
	/// Prefix + guard: the drag-drop path (PlayerCamera.cs:1623) calls DropItem
	/// with the dragged item ALREADY un-slotted (HoldingItem false) — DropItem is
	/// a no-op then, but the old Postfix reported a drop anyway. The peer
	/// materialized an item that was never dropped, and when the follow-up
	/// PickUpItem failed (DoPickupCheck sight/distance) the phantom never left
	/// ("extra item that does not disappear"). Report only real drops; the
	/// no-op call runs (harmless) without a report. The position read is the
	/// slot position, identical to the post-drop one (DropItem only re-parents).
	/// </summary>
	[HarmonyPatch(typeof(Body), "DropItem", new Type[] { typeof(Item) })]
	internal static class DropItemPatch
	{
		private static void Prefix(Body __instance, Item item)
		{
			if (!SwapSlotsPatch.Swapping && __instance.HoldingItem(item))
			{
				PatchBridge.Impl?.OnItemDropped(item);
			}
		}
	}

	/// <summary>Same guard for DropWearable — its GetWearable check can no-op (not worn), which the old Postfix reported as a drop.</summary>
	[HarmonyPatch(typeof(Body), "DropWearable")]
	internal static class DropWearablePatch
	{
		private static void Prefix(Body __instance, Item item)
		{
			if (__instance.GetWearable(item.id) != null) // Unity object — ==
			{
				PatchBridge.Impl?.OnItemDropped(item);
			}
		}
	}
}
