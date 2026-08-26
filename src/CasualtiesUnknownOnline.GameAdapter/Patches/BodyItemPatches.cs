using System;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Items;
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
	/// The scope crosses Prefix → Postfix via Harmony __state (per-call state,
	/// never a static field — Harmony does not dispose it, the postfix does).
	/// </summary>
	[HarmonyPatch(typeof(Body), "SwapSlots")]
	internal static class SwapSlotsPatch
	{
		private static void Prefix(out IDisposable __state) => __state = CallContext.Enter(CallContext.Origin.InternalReorder);

		// An inventory-internal move — no world events, but the peer's clone
		// must re-render in real time (the 1 Hz character throttle alone reads
		// as a 1-2 s delay), and the host's transfer-table record must follow
		// the new slots (the slot report is id + slot — power-idempotent, the
		// host's record ends at the latest).
		private static void Postfix(Body __instance, IDisposable __state, int slot1, int slot2)
		{
			__state.Dispose();
			PatchBridge.Impl?.OnInventoryChanged();
			PatchBridge.Impl?.OnSlotMoved(__instance, slot1, "Swap");
			PatchBridge.Impl?.OnSlotMoved(__instance, slot2, "Swap");
		}
	}

	/// <summary>SwitchHands drops both hands and picks them back (Body.cs:1113-1133) — an internal swap, not world events; same origin scope as SwapSlots.</summary>
	[HarmonyPatch(typeof(Body), "SwitchHands")]
	internal static class SwitchHandsPatch
	{
		private static void Prefix(out IDisposable __state) => __state = CallContext.Enter(CallContext.Origin.InternalReorder);

		private static void Postfix(Body __instance, IDisposable __state)
		{
			__state.Dispose();
			PatchBridge.Impl?.OnInventoryChanged();
			PatchBridge.Impl?.OnSlotMoved(__instance, 0, "Hands");
			PatchBridge.Impl?.OnSlotMoved(__instance, 1, "Hands");
		}
	}

	[HarmonyPatch(typeof(Body), "PickUpItem")]
	internal static class PickUpItemPatch
	{
		private static void Prefix(Item item) => PatchBridge.Impl?.OnItemPickupStart(item);

		// Only a pickup that actually landed (the guard clauses inside PickUpItem
		// — slot capacity, distance — can fail and leave the item untouched);
		// slot-to-slot moves (SwapSlots/SwitchHands) are inventory-internal
		// reorders, not world events — their scope tells us, no static flags. A
		// Craft scope silences the report too: a product's pickup rides the ONE
		// craft report (the coordinator's inventory diff), never a per-call one.
		private static void Postfix(Body __instance, Item item)
		{
			if (CallContext.Current != CallContext.Origin.InternalReorder
				&& CallContext.Current != CallContext.Origin.Craft
				&& __instance.HoldingItem(item))
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
			if (CallContext.Current != CallContext.Origin.InternalReorder
				&& CallContext.Current != CallContext.Origin.Craft // a destroyed material's DropItem (RecipeItem.cs:182) — its fact rides the craft report
				&& __instance.HoldingItem(item))
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
	/// ThrowItem also plays the ArmsSwing clip (Body.cs:1665) — the swing is
	/// reported so the peer's clone replays it (IsAttacking snapshot flag).
	/// The item crosses Prefix → Postfix via Harmony __state (per-call state).
	/// </summary>
	[HarmonyPatch(typeof(Body), "ThrowItem")]
	internal static class ThrowItemPatch
	{
		private sealed class ThrowState
		{
			internal Item? Item;
			internal IDisposable? SoundScope;
		}

		private static void Prefix(Body __instance, out ThrowState __state)
		{
			// The character-sound capture scope: the native ThrowItem plays its
			// BSSwing clip inside this scope. A render clone never throws — no
			// scope, no capture.
			__state = new ThrowState
			{
				Item = __instance.GetItem(__instance.handSlot),
				SoundScope = __instance.GetComponentInParent<RemoteBodyDriver>() == null
					? CallContext.Enter(CallContext.Origin.CharacterThrow)
					: null,
			};
		}

		private static void Postfix(ThrowState __state)
		{
			__state.SoundScope?.Dispose();
			if (__state.Item != null) // Unity object — == (a throw only runs with an item, Body.cs:1654)
			{
				PatchBridge.Impl?.OnArmSwing();
				PatchBridge.Impl?.OnItemThrown(__state.Item);
			}
		}
	}

	/// <summary>
	/// The direct placeable-item use actions (scrapmetal / climbingrope /
	/// scaffoldingpack, Item.cs:2143-2250) play <c>body.armsAnimator.Play(
	/// "ArmsSwing")</c> themselves instead of going through
	/// <c>Body.Attack</c>/<c>Body.ThrowItem</c>, so the existing
	/// <see cref="IPatchBridge.OnArmSwing"/> report never fired for them.
	/// This patch reports a successful placeable use after the native action:
	/// the success signal is the item-condition reduction written by the same
	/// action (scrapmetal 0.25, climbingrope 0.501, scaffoldingpack 0.01), so
	/// gated/failed attempts (canPlaceBlock false, occupied target, low
	/// condition) are not marked as swings. It rides the existing
	/// IsAttacking / SwingSeq 20 Hz entity stream — no new wire message.
	/// </summary>
	[HarmonyPatch(typeof(Body), "UseItem")]
	internal static class DirectPlaceableUseItemPatch
	{
		private static void Prefix(Body __instance, Item item, out float __state) =>
			__state = item.condition;

		private static void Postfix(Body __instance, Item item, float __state)
		{
			if (IsEligibleLocalUse(__instance)
				&& DirectPlaceableArmSwingPolicy.ShouldReport(item.id, __state, item.condition))
			{
				PatchBridge.Impl?.OnArmSwing();
			}
		}
	}

	/// <summary>
	/// The left-click hand-use path (Body.cs:2449-2455) calls
	/// <c>Stats.useAction</c> directly, not <c>Body.UseItem</c>, so the
	/// <see cref="DirectPlaceableUseItemPatch"/> alone would miss the normal
	/// placeable LMB action. This second hook covers the whole direct
	/// placeable-item family before the swing reaches the peers.
	/// </summary>
	[HarmonyPatch(typeof(Body), "UseItemInHand")]
	internal static class DirectPlaceableUseItemInHandPatch
	{
		private static void Prefix(Body __instance, out float __state)
		{
			var item = __instance.GetItem(__instance.handSlot);
			__state = item != null ? item.condition : -1f; // Unity object — ==
		}

		private static void Postfix(Body __instance, float __state)
		{
			var item = __instance.GetItem(__instance.handSlot);
			if (item != null // Unity object — ==
				&& IsEligibleLocalUse(__instance)
				&& DirectPlaceableArmSwingPolicy.ShouldReport(item.id, __state, item.condition))
			{
				PatchBridge.Impl?.OnArmSwing();
			}
		}
	}

	private static bool IsEligibleLocalUse(Body body) =>
		body.GetComponentInParent<RemoteBodyDriver>() == null
		&& !CarriedBodyDriver.IsCarrying(body) // Unity objects — ==
		&& CallContext.Current == CallContext.Origin.LocalAction;
}
