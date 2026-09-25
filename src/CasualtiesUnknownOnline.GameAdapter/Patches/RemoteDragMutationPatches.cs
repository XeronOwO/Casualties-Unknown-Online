using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The mutation seams of the remote display-proxy release window. The game's own
/// release branch runs on the viewer's client; each native mutation entry point
/// it can reach asks the window first, and the original call is skipped whenever
/// the window took it — so a display proxy is never mutated, and the call the
/// native branch made becomes the intent the owner replays.
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
	/// <c>Container.UnloadItem</c> inside the release window: the first half of the
	/// native move pair, or — with no load on that container behind it — the take
	/// into the world (W1). The container-expansion batch (R5) unloads each child
	/// of the dragged container, which the window records as a gesture this stage
	/// cannot carry yet.
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
	/// <c>Body.CombineItems</c> inside the release window. The native branch
	/// combines the hovered slot item with the dragged item; with a display proxy in
	/// either position that is a cross-item interaction whose own intent arrives in
	/// a later stage, so the call is refused and never runs on a proxy.
	/// </summary>
	[HarmonyPatch(typeof(Body), "CombineItems")]
	internal static class RemoteDragBodyCombinePatch
	{
		private static bool Prefix()
		{
			var window = RemoteDragIntentWindow.Current;
			if (!window.IsOpen)
			{
				return true;
			}

			window.Refuse("combine two items through the remote view (item-interaction intents arrive in a later stage)");
			return false;
		}
	}

	/// <summary>Battery load inside the release window: a battery interaction across two items, carried by a later stage's intent.</summary>
	[HarmonyPatch(typeof(BatteryItem), "LoadBattery")]
	internal static class RemoteDragBatteryLoadPatch
	{
		private static bool Prefix()
		{
			var window = RemoteDragIntentWindow.Current;
			if (!window.IsOpen)
			{
				return true;
			}

			window.Refuse("battery load through the remote view (item-interaction intents arrive in a later stage)");
			return false;
		}
	}

	/// <summary>Battery unload inside the release window: same family as <see cref="RemoteDragBatteryLoadPatch"/>.</summary>
	[HarmonyPatch(typeof(BatteryItem), "UnloadBattery")]
	internal static class RemoteDragBatteryUnloadPatch
	{
		private static bool Prefix()
		{
			var window = RemoteDragIntentWindow.Current;
			if (!window.IsOpen)
			{
				return true;
			}

			window.Refuse("battery unload through the remote view (item-interaction intents arrive in a later stage)");
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

	/// <summary>Trader hand-in inside the release window: the gesture's intent is not carried yet, so it is refused instead of handing a proxy to the trader.</summary>
	[HarmonyPatch(typeof(TraderScript), "GiveItem")]
	internal static class RemoteDragTraderGivePatch
	{
		private static bool Prefix()
		{
			var window = RemoteDragIntentWindow.Current;
			if (!window.IsOpen)
			{
				return true;
			}

			window.Refuse("give to trader through the remote view (no intent for this gesture yet)");
			return false;
		}
	}
}
