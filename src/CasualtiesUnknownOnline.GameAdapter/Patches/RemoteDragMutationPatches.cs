using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The mutation seams of the remote display-proxy drag windows. The game's own
/// drag branch runs on the viewer's client; each native mutation entry point it can
/// reach asks the window first, and the original call is skipped whenever the
/// window took it — so a display proxy is never mutated, and the call the native
/// body made becomes the intent the owner replays. The release bracket carries the
/// discrete intents; the while-dragging bracket carries the continuous ones.
/// </summary>
internal static class RemoteDragMutationPatches
{
	[HarmonyPatch(typeof(Container), "LoadItem")]
	internal static class RemoteDragContainerLoadPatch
	{
		private static bool Prefix(Container __instance, Item item)
		{
			var window = RemoteDragIntentWindow.Current;
			if (!window.IsOpen)
			{
				return true;
			}

			if (!RemoteDragProxyQuery.IsProxy(item) && !RemoteDragProxyQuery.IsProxy(__instance))
			{
				return true;
			}

			window.CaptureContainerLoad(RemoteDragProxyQuery.InstanceId(item), RemoteDragProxyQuery.InstanceId(__instance.GetComponent<Item>()));
			return false;
		}
	}

	/// <summary>
	/// <c>Container.UnloadItem</c> inside a drag window: the first half of the
	/// native move pair, or — with no load on that container behind it — the take
	/// into the world (W1). The container-expansion loop (R5) unloads each child
	/// out of the dragged item's OWN container, which the window records as the
	/// batch's first half.
	/// </summary>
	[HarmonyPatch(typeof(Container), "UnloadItem")]
	internal static class RemoteDragContainerUnloadPatch
	{
		private static bool Prefix(Container __instance, Item item)
		{
			var window = RemoteDragIntentWindow.Current;
			if (!window.IsOpen)
			{
				return true;
			}

			if (!RemoteDragProxyQuery.IsProxy(item) && !RemoteDragProxyQuery.IsProxy(__instance))
			{
				return true;
			}

			window.CaptureContainerUnload(RemoteDragProxyQuery.InstanceId(item), RemoteDragProxyQuery.InstanceId(__instance.GetComponent<Item>()));
			return false;
		}
	}

	/// <summary>
	/// <c>WaterContainerItem.Drain</c> from the while-dragging body's liquid tick
	/// (<c>PlayerCamera.cs:1729</c> gates it, <c>:1731-1732</c> calls it). The caller
	/// hands over the per-stack list it computed from the PROXY's stack; the intent
	/// carries the amount that list removes instead, because the owner must derive the
	/// distribution from the stack its own item really has. The local call is skipped
	/// — a display proxy is never mutated — and the owner's facts reach this client
	/// through the projection.
	///
	/// A proxy that NO open while-dragging bracket took is refused instead of run:
	/// that is the proxy carrying no authoritative identity (the release path fails
	/// closed for the same shape), or a bracket that belongs to another item. The
	/// native call would mutate a display proxy for a gesture that cannot become an
	/// intent, so it is skipped with one line per dragged proxy.
	/// </summary>
	[HarmonyPatch(typeof(WaterContainerItem), "Drain")]
	internal static class RemoteDragLiquidDrainPatch
	{
		/// <summary>The proxy whose refusal was last reported — one line per drag, not one per frame.</summary>
		private static int _reportedItemInstanceId;

		private static bool Prefix(WaterContainerItem __instance, List<float> toRemove)
		{
			var item = __instance.GetComponent<Item>();
			if (item == null || !RemoteDragProxyQuery.IsProxy(item)) // Unity object — ==
			{
				// A real local item: the native call is the local gesture.
				return true;
			}

			var window = RemoteDragIntentWindow.Current;
			var itemId = RemoteDragProxyQuery.InstanceId(item);
			if (!window.CapturesContinuousCallsFor(itemId))
			{
				ReportNotOperableOnce(item);
				return false;
			}

			if (toRemove is null)
			{
				window.Refuse("a liquid drain tick without its removal list");
				return false;
			}

			var amount = 0f;
			foreach (var part in toRemove)
			{
				amount += part;
			}

			window.CaptureDrain(itemId, amount);
			return false;
		}

		private static void ReportNotOperableOnce(Item item)
		{
			var instanceId = item.GetInstanceID();
			if (instanceId == _reportedItemInstanceId)
			{
				return;
			}

			_reportedItemInstanceId = instanceId;
			PatchBridge.Impl?.ReportProxyNotOperable(item, "the liquid drain tick");
		}
	}

	/// <summary>
	/// <c>Body.PickUpItem</c> inside the release window: R9's slot release. Which
	/// body the slot belongs to comes from the window (the ring shows the owner's
	/// displayed body while the remote view is open), so the intent is either a
	/// pick-up on the owner or the cross-player transfer onto the requester.
	/// </summary>
	[HarmonyPatch(typeof(Body), "PickUpItem")]
	internal static class RemoteDragBodyPickUpPatch
	{
		private static bool Prefix(Item item, int slot)
		{
			var window = RemoteDragIntentWindow.Current;
			if (!window.IsOpen || !RemoteDragProxyQuery.IsProxy(item))
			{
				return true;
			}

			window.CapturePickUp(RemoteDragProxyQuery.InstanceId(item), slot);
			return false;
		}
	}

	/// <summary>
	/// <c>Body.SwapSlots</c> inside the release window (R8): the native guard can
	/// only pass while the ring reads the owner's displayed body, so the swapped
	/// slots are the owner's. The owner resolves the item's own slot itself.
	/// </summary>
	[HarmonyPatch(typeof(Body), "SwapSlots")]
	internal static class RemoteDragBodySwapSlotsPatch
	{
		private static bool Prefix(Body __instance, int slot1, int slot2)
		{
			var window = RemoteDragIntentWindow.Current;
			if (!window.IsOpen || !window.DestinationIsOwnerBody)
			{
				return true;
			}

			// The native call is SwapSlots(invButton.slot, body.SlotOf(dragItem))
			// (PlayerCamera.cs:1616), so the swapped item is the one in slot2 — read
			// through the redirect, that is the display proxy itself.
			var item = __instance.GetItem(slot2);
			if (item == null)
			{
				window.Refuse($"slot swap in slots {slot1}/{slot2} without a resolvable item");
				return false;
			}

			window.CaptureSwapSlots(RemoteDragProxyQuery.InstanceId(item), slot1);
			return false;
		}
	}

	/// <summary><c>Body.DropItem(Item)</c> inside the release window: R9's own first step or the world drop (W2).</summary>
	[HarmonyPatch(typeof(Body), "DropItem", [typeof(Item)])]
	internal static class RemoteDragBodyDropItemPatch
	{
		private static bool Prefix(Item item)
		{
			var window = RemoteDragIntentWindow.Current;
			if (!window.IsOpen || !RemoteDragProxyQuery.IsProxy(item))
			{
				return true;
			}

			window.CaptureDropItem(RemoteDragProxyQuery.InstanceId(item));
			return false;
		}
	}

	/// <summary>
	/// <c>Body.DropItem(int)</c> inside the release window with the ring on the
	/// owner's body: R9's occupying-slot step, resolved against the body the ring
	/// shows. The intent's owner-side execution performs it there, so the call is
	/// absorbed instead of dropping an item of the requester's own body.
	/// </summary>
	[HarmonyPatch(typeof(Body), "DropItem", [typeof(int)])]
	internal static class RemoteDragBodyDropSlotPatch
	{
		private static bool Prefix()
		{
			var window = RemoteDragIntentWindow.Current;
			return !(window.IsOpen && window.DestinationIsOwnerBody);
		}
	}

	/// <summary><c>Body.DropWearable</c> inside the release window (W3).</summary>
	[HarmonyPatch(typeof(Body), "DropWearable")]
	internal static class RemoteDragBodyDropWearablePatch
	{
		private static bool Prefix(Item item)
		{
			var window = RemoteDragIntentWindow.Current;
			if (!window.IsOpen || !RemoteDragProxyQuery.IsProxy(item))
			{
				return true;
			}

			window.CaptureDropWearable(RemoteDragProxyQuery.InstanceId(item));
			return false;
		}
	}

	/// <summary>
	/// <c>Body.CombineItems</c> inside the release window (R6). The native branch
	/// combines the hit item with the dragged item and the FIRST argument is the
	/// receiver — a gun takes the magazine, a container with space takes the liquid,
	/// the condition merge credits <c>it1</c> — so the intent names both operands and
	/// the owner evaluates the call on its own real pair.
	/// </summary>
	[HarmonyPatch(typeof(Body), "CombineItems")]
	internal static class RemoteDragBodyCombinePatch
	{
		private static bool Prefix(Item it1, Item it2)
		{
			var window = RemoteDragIntentWindow.Current;
			if (!window.IsOpen
				|| (!RemoteDragProxyQuery.IsProxy(it1) && !RemoteDragProxyQuery.IsProxy(it2)))
			{
				return true;
			}

			// The native call is CombineItems(hitItem, dragItem)
			// (PlayerCamera.cs:1602): it1 is the hit item and it2 the dragged proxy.
			window.CaptureCombine(
				RemoteDragProxyQuery.InstanceId(it2),
				RemoteDragProxyQuery.InstanceId(it1),
				RemoteDragProxyQuery.OwnerSteamId(it1));
			return false;
		}
	}

	/// <summary><c>Body.UseItem</c> inside the release window (R10, <c>PlayerCamera.cs:1646</c>): the radial-centre use of the dragged proxy, replayed by the owner on its real item.</summary>
	[HarmonyPatch(typeof(Body), "UseItem")]
	internal static class RemoteDragBodyUseItemPatch
	{
		private static bool Prefix(Item item)
		{
			var window = RemoteDragIntentWindow.Current;
			if (!window.IsOpen || !RemoteDragProxyQuery.IsProxy(item))
			{
				return true;
			}

			window.CaptureUseItem(RemoteDragProxyQuery.InstanceId(item));
			return false;
		}
	}

	/// <summary>
	/// <c>Body.WearWearable</c> inside the release window (R10,
	/// <c>PlayerCamera.cs:1642</c>). The native branch tests <c>wearable</c> and
	/// <c>usable</c> as two independent <c>if</c>s, so this call and
	/// <see cref="RemoteDragBodyUseItemPatch"/> can both fire inside ONE release:
	/// that pair is the native order, not a duplicate.
	/// </summary>
	[HarmonyPatch(typeof(Body), "WearWearable")]
	internal static class RemoteDragBodyWearPatch
	{
		private static bool Prefix(Item item)
		{
			var window = RemoteDragIntentWindow.Current;
			if (!window.IsOpen || !RemoteDragProxyQuery.IsProxy(item))
			{
				return true;
			}

			window.CaptureWearItem(RemoteDragProxyQuery.InstanceId(item));
			return false;
		}
	}

	/// <summary>
	/// Battery load inside the release window (R3): the dragged battery item goes
	/// into the HIT item's battery slot, so the intent carries both — the battery is
	/// consumed there and the receiving item is charged.
	/// </summary>
	[HarmonyPatch(typeof(BatteryItem), "LoadBattery")]
	internal static class RemoteDragBatteryLoadPatch
	{
		private static bool Prefix(BatteryItem __instance, Item battery)
		{
			var window = RemoteDragIntentWindow.Current;
			if (!window.IsOpen
				|| (!RemoteDragProxyQuery.IsProxy(battery) && !RemoteDragProxyQuery.IsProxy(__instance)))
			{
				return true;
			}

			var target = __instance.GetComponent<Item>();
			window.CaptureBatteryLoad(
				RemoteDragProxyQuery.InstanceId(battery),
				RemoteDragProxyQuery.InstanceId(target),
				RemoteDragProxyQuery.OwnerSteamId(target));
			return false;
		}
	}

	/// <summary>
	/// Battery unload inside the release window (R2, <c>PlayerCamera.cs:1545</c>):
	/// the HIT item's own battery is ejected. The native call reads nothing else —
	/// the dragged item only told the dispatch the direction — so the hit item is
	/// the single operand. A call on an item that is not a display proxy is a local
	/// battery unload and keeps the native path.
	/// </summary>
	[HarmonyPatch(typeof(BatteryItem), "UnloadBattery")]
	internal static class RemoteDragBatteryUnloadPatch
	{
		private static bool Prefix(BatteryItem __instance)
		{
			var window = RemoteDragIntentWindow.Current;
			if (!window.IsOpen || !RemoteDragProxyQuery.IsProxy(__instance))
			{
				return true;
			}

			var target = __instance.GetComponent<Item>();
			window.CaptureBatteryUnload(
				RemoteDragProxyQuery.InstanceId(target),
				RemoteDragProxyQuery.OwnerSteamId(target));
			return false;
		}
	}

	/// <summary>
	/// <c>PlayerCamera.ApplyWoundItem</c> inside the release window (R11): the
	/// dragged proxy is applied to a limb of the acting body, which is the landed
	/// held-remote-item medical flow. The intent carries the limb the wound view is
	/// pointing at, exactly as the native code reads it.
	/// </summary>
	[HarmonyPatch(typeof(PlayerCamera), "ApplyWoundItem")]
	internal static class RemoteDragApplyWoundItemPatch
	{
		private static bool Prefix(PlayerCamera __instance)
		{
			var window = RemoteDragIntentWindow.Current;
			if (!window.IsOpen)
			{
				return true;
			}

			var woundView = __instance.woundView != null // Unity object — ==
				? __instance.woundView.GetComponent<WoundView>()
				: null; // Unity object — ==
			window.CaptureApplyToLimb(window.DraggedItemId, woundView != null ? woundView.limbLookingAt : -1);
			return false;
		}
	}

	/// <summary>
	/// Trader hand-in inside the release window (R12, <c>PlayerCamera.cs:1663</c>):
	/// the dragged proxy is handed to the trader the requester has the trade menu
	/// open on. The trader is an operand because the OWNER's client has no
	/// <c>PlayerCamera.currentTrader</c>: the intent carries the trader's world
	/// position, the identity the trade domain already keys its messages by
	/// (<c>TraderSwingMsg.Position</c>, <c>TradeStateSync.FindTraderAt</c>).
	/// </summary>
	[HarmonyPatch(typeof(TraderScript), "GiveItem")]
	internal static class RemoteDragTraderGivePatch
	{
		private static bool Prefix(TraderScript __instance, Item item)
		{
			var window = RemoteDragIntentWindow.Current;
			if (!window.IsOpen || !RemoteDragProxyQuery.IsProxy(item))
			{
				return true;
			}

			var position = __instance.transform.position;
			window.CaptureGiveToTrader(
				RemoteDragProxyQuery.InstanceId(item),
				new NetVector2Msg(position.x, position.y));
			return false;
		}
	}
}
