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
	/// Scope while Body.SwapSlots re-parents items between slots (the drag UI):
	/// it internally drops and picks up both items, but nothing left the world —
	/// the reports would be false "placed"/"picked up" broadcasts. SwitchHands
	/// (Body.cs:1113) is the same internal drop+pick pair and shares the origin.
	/// The scope holder is a static field only because Prefix/Postfix are two
	/// separate callbacks — the step-6 patch slimming moves it to __state.
	/// </summary>
	[HarmonyPatch(typeof(Body), "SwapSlots")]
	internal static class SwapSlotsPatch
	{
		// step-6 fodder: becomes "out IDisposable __state" with the patch slimming
		private static IDisposable? _swapScope;

		private static void Prefix() => _swapScope = CallContext.Enter(CallContext.Origin.InternalReorder);

		// An inventory-internal move — no world events, but the peer's clone
		// must re-render in real time (the 1 Hz character throttle alone reads
		// as a 1-2 s delay).
		private static void Postfix()
		{
			_swapScope?.Dispose();
			_swapScope = null;
			PatchBridge.Impl?.OnInventoryChanged();
		}
	}

	/// <summary>SwitchHands drops both hands and picks them back (Body.cs:1113-1133) — an internal swap, not world events; same origin scope as SwapSlots.</summary>
	[HarmonyPatch(typeof(Body), "SwitchHands")]
	internal static class SwitchHandsPatch
	{
		// step-6 fodder: becomes "out IDisposable __state" with the patch slimming
		private static IDisposable? _switchScope;

		private static void Prefix() => _switchScope = CallContext.Enter(CallContext.Origin.InternalReorder);

		private static void Postfix()
		{
			_switchScope?.Dispose();
			_switchScope = null;
			PatchBridge.Impl?.OnInventoryChanged();
		}
	}

	[HarmonyPatch(typeof(Body), "PickUpItem")]
	internal static class PickUpItemPatch
	{
		private static void Prefix(Item item) => PatchBridge.Impl?.OnItemPickupStart(item);

		// Only a pickup that actually landed (the guard clauses inside PickUpItem
		// — slot capacity, distance — can fail and leave the item untouched);
		// slot-to-slot moves (SwapSlots/SwitchHands) are inventory-internal
		// reorders, not world events — their scope tells us, no static flags.
		private static void Postfix(Body __instance, Item item)
		{
			if (CallContext.Current != CallContext.Origin.InternalReorder && __instance.HoldingItem(item))
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
			if (CallContext.Current != CallContext.Origin.InternalReorder && __instance.HoldingItem(item))
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

	/// <summary>
	/// A throw's drop report fired in the DropItem prefix — before ThrowItem
	/// set the throw velocity (Body.cs:1659-1661) — so the peer's copy dropped
	/// in place. Re-report with the real flight velocity after ThrowItem ran.
	/// The item is captured in the prefix (Harmony cannot inject a parameter
	/// the original method does not have — an unmatched parameter fails the
	/// WHOLE PatchAll).
	/// </summary>
	[HarmonyPatch(typeof(Body), "ThrowItem")]
	internal static class ThrowItemPatch
	{
		private static Item? _thrown;

		private static void Prefix(Body __instance) => _thrown = __instance.GetItem(__instance.handSlot);

		private static void Postfix()
		{
			if (_thrown != null) // Unity object — ==
			{
				PatchBridge.Impl?.OnItemThrown(_thrown);
				_thrown = null;
			}
		}
	}
}
