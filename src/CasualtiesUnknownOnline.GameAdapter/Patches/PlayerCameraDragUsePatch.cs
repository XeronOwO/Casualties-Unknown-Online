using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Cross-player native drag release. The seams covered here:
/// (1) remote-backpack take — while the native remote backpack view is open,
/// dragging a display-proxy item out is a host-authoritative take request, never
/// a local body mutation; (2) remote-backpack pour/drop/container gestures — the
/// same release is mapped to host-authoritative semantic operations instead of
/// mutating a display proxy; (3) KrokMP-style cross-player item use by drag —
/// when the native drag release happens over an in-world remote player, route
/// the dragged usable item to the existing cross-player use request and skip the
/// native drop path; (4) Tab-switch transfer — a remote proxy released into the
/// local inventory after the remote view closed becomes the existing take
/// request. Remote clones have no colliders, so overlap is world-space around
/// the authoritative stream position.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleReleaseDragging")]
internal static class PlayerCameraDragUsePatch
{
	private static bool Prefix(PlayerCamera __instance, List<RaycastResult> uiCasts)
	{
		// The remote medical view owns its own drag-to-limb treatment routing.
		// Let the native UI reach TryPerformSpecialUIAction so the WoundView
		// limb gesture is consumed by RemoteMedicalPatches instead of being
		// preempted by the world-overlap cross-player use path. If the remote
		// backpack is somehow also open, keep the backpack proxy protections
		// ahead of the medical path.
		if (RemoteMedicalView.IsOpen && !RemoteBackpackView.IsOpen)
		{
			return true;
		}

		if (RemoteBackpackView.IsOpen)
		{
			// Every named remote-backpack native gesture is mapped to a
			// host-authoritative request first. Only when no specific gesture
			// matched do we fall back to the legacy remote-take path, so a
			// container/center/slot release is never swallowed as a take.
			if (IsRemoteProxy(__instance.dragItem))
			{
				// Craft/container windows are pure local UI on a display proxy:
				// they do not need a host authority request, and they must not
				// fall into the old native release path (which could unload a
				// remote container proxy).
				if (TryHandleRemoteUiOnlyGesture(__instance, __instance.dragItem, uiCasts))
				{
					ClearDrag(__instance);
					return false;
				}

				if (TryHandleRemoteProxyRelease(__instance.dragItem, uiCasts))
				{
					ClearDrag(__instance);
					return false;
				}

				if (TryHandleRemoteBackpackTake(__instance.dragItem, uiCasts))
				{
					ClearDrag(__instance);
					return false;
				}
			}

			// The remote backpack surface may only be consumed by the dedicated
			// host-authoritative operations below. Any other dragged item (a
			// world/local item picked up while the view is open) must be dropped
			// rather than allowed to mutate the display clone through the
			// original release path.
			if (__instance.dragItem != null) // Unity object — ==
			{
				CancelDrag(__instance, "remote backpack view did not consume the drag");
				return false;
			}

			return true;
		}

		// A display proxy picked up from the remote view is the only drag that
		// can legally outlive that view. It may be consumed by the remote-take
		// path OR by a Tab-switch transfer into the local inventory; any other
		// release must be cancelled before the original native release or the
		// cross-player use path can move it into an authoritative inventory.
		if (RemoteProxyDragPolicy.ShouldCancelProxyRelease(IsRemoteProxy(__instance.dragItem), remoteTakeHandled: false)
			&& TryHandleRemoteProxyTransferToLocalOverLocalInventory(__instance.dragItem, uiCasts))
		{
			ClearDrag(__instance);
			return false;
		}

		if (RemoteProxyDragPolicy.ShouldCancelProxyRelease(IsRemoteProxy(__instance.dragItem), remoteTakeHandled: false))
		{
			CancelDrag(__instance, "remote display proxy released outside the remote backpack view");
			return false;
		}

		if (PatchBridge.Impl?.TryHandleDraggedItemUseOnRemote(__instance.dragItem, __instance.body) == true)
		{
			ClearDrag(__instance);
			return false;
		}

		return true;
	}

	/// <summary>
	/// UI-only remote-proxy gestures: opening the crafting screen from a dragged
	/// remote item and opening a remote container's window. Both are
	/// presentation-only on display proxies and intentionally do not travel to
	/// the host — the owner's real inventory is never mutated by these actions.
	/// </summary>
	private static bool TryHandleRemoteUiOnlyGesture(PlayerCamera camera, Item dragItem, List<RaycastResult> uiCasts)
	{
		foreach (var raycastResult in uiCasts)
		{
			if (raycastResult.gameObject == camera.craftButton) // Unity object — ==
			{
				camera.OpenCraftScreen();
				camera.SeeRecipesWithItem(dragItem);
				return true;
			}
		}

		var container = dragItem.GetComponent<Container>();
		if (container != null // Unity object — ==
			&& Vector2.Distance(Input.mousePosition, camera.clickPos) < 10f)
		{
			camera.OpenContainer(container);
			return true;
		}

		return false;
	}

	/// <summary>
	/// Route one remote display-proxy release while the remote view is open.
	/// Named gestures are ordered like the native inventory UI: container
	/// move, radial centre use/wear, inventory-button battery/combine/slot
	/// actions, then pour and edge drop. A release that matches no named
	/// gesture falls through to the legacy remote-take fallback.
	/// </summary>
	private static bool TryHandleRemoteProxyRelease(Item dragItem, List<RaycastResult> uiCasts)
	{
		if (TryFindRemoteContainerTarget(uiCasts, out var target))
		{
			return PatchBridge.Impl?.TryHandleRemoteBackpackMoveToContainer(dragItem, target) == true;
		}

		if (TryHandleRadialCenter(dragItem, uiCasts))
		{
			return true;
		}

		if (TryHandleInventoryButton(dragItem, uiCasts))
		{
			return true;
		}

		if (IsPourGesture(dragItem))
		{
			return PatchBridge.Impl?.TryHandleRemoteBackpackPour(dragItem) == true;
		}

		if (IsEdgeDrop())
		{
			return PatchBridge.Impl?.TryHandleRemoteBackpackDrop(dragItem) == true;
		}

		return false;
	}

	// The fallback is the pre-existing take request. It is only reached after
	// every named native gesture failed to match, so it can never swallow a
	// container/centre/slot release anymore.
	private static bool TryHandleRemoteBackpackTake(Item dragItem, List<RaycastResult> uiCasts) =>
		PatchBridge.Impl?.TryHandleRemoteBackpackTake(dragItem) == true;

	private static bool TryHandleRadialCenter(Item dragItem, List<RaycastResult> uiCasts)
	{
		foreach (var raycastResult in uiCasts)
		{
			if (!raycastResult.gameObject.CompareTag("RadialCenter"))
			{
				continue;
			}

			var bridge = PatchBridge.Impl;
			if (dragItem.Stats.wearable && bridge?.TryHandleRemoteBackpackWear(dragItem) == true)
			{
				return true;
			}

			if (dragItem.Stats.usable && bridge?.TryHandleRemoteBackpackUse(dragItem) == true)
			{
				return true;
			}

			// The radial centre is a named drop target even when the dragged
			// item is neither wearable nor usable; consume the release so it is
			// never misrouted as a take.
			return true;
		}

		return false;
	}

	private static bool TryHandleInventoryButton(Item dragItem, List<RaycastResult> uiCasts)
	{
		foreach (var raycastResult in uiCasts)
		{
			var button = raycastResult.gameObject.GetComponent<InvButton>();
			if (button == null || !button.Overlaps(uiCasts)) // Unity object — ==
			{
				continue;
			}

			var target = button.GetItem();
			if (target == null)
			{
				return button.isBody
					&& PatchBridge.Impl?.TryHandleRemoteBackpackMoveToSlot(dragItem, button.slot) == true;
			}

			if (!IsRemoteProxy(target))
			{
				continue;
			}

			if (TryHandleBattery(dragItem, target))
			{
				return true;
			}

			if (target.GetComponent<Container>() != null) // Unity object — ==
			{
				return PatchBridge.Impl?.TryHandleRemoteBackpackMoveToContainer(dragItem, target) == true;
			}

			if (CanCombineRemote(dragItem, target))
			{
				return PatchBridge.Impl?.TryHandleRemoteBackpackCombine(dragItem, target) == true;
			}

			if (button.isBody)
			{
				return PatchBridge.Impl?.TryHandleRemoteBackpackMoveToSlot(dragItem, button.slot) == true;
			}

			return false;
		}

		return false;
	}

	private static bool TryHandleBattery(Item dragItem, Item target)
	{
		if (target.battery == null) // Unity object — ==
		{
			return false;
		}

		var bridge = PatchBridge.Impl;
		if (dragItem.Stats.HasTag("battery"))
		{
			return bridge?.TryHandleRemoteBackpackBatteryLoad(dragItem, target) == true;
		}

		if (dragItem.Stats.HasTag("tool"))
		{
			return bridge?.TryHandleRemoteBackpackBatteryUnload(dragItem, target) == true;
		}

		return false;
	}

	private static bool CanCombineRemote(Item dragItem, Item target)
	{
		var focused = RemoteBackpackView.FocusedBody;
		return focused != null && focused.CanCombine(target, dragItem); // Unity object — ==
	}

	private static bool TryHandleRemoteProxyTransferToLocalOverLocalInventory(Item dragItem, List<RaycastResult> uiCasts)
	{
		if (!IsRemoteProxy(dragItem) || !IsLocalInventoryRelease(uiCasts))
		{
			return false;
		}

		return PatchBridge.Impl?.TryHandleRemoteProxyTransferToLocal(dragItem) == true;
	}

	private static bool TryFindRemoteContainerTarget(List<RaycastResult> uiCasts, out Item target)
	{
		foreach (var raycastResult in uiCasts)
		{
			var button = raycastResult.gameObject.GetComponent<InvButton>();
			if (button == null || !button.Overlaps(uiCasts)) // Unity object — ==
			{
				continue;
			}

			var item = button.GetItem();
			if (item != null && item.GetComponent<RemoteCloneRender>() != null // Unity objects — ==
				&& item.GetComponent<Container>() != null) // Unity object — ==
			{
				target = item;
				return true;
			}
		}

		target = null!;
		return false;
	}

	private static bool IsPourGesture(Item dragItem)
	{
		if (dragItem.GetComponent<WaterContainerItem>() == null) // Unity object — ==
		{
			return false;
		}

		return Input.mousePosition.x < 100f;
	}

	private static bool IsEdgeDrop()
	{
		var x = Input.mousePosition.x;
		return x < 100f || x > Screen.width - 100f;
	}

	private static bool IsLocalInventoryRelease(List<RaycastResult> uiCasts)
	{
		foreach (var raycastResult in uiCasts)
		{
			if (raycastResult.gameObject.GetComponent<InvButton>() != null) // Unity object — ==
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsRemoteProxy(Item? dragItem) =>
		dragItem != null && dragItem.GetComponent<RemoteCloneRender>() != null; // Unity objects — ==

	private static void CancelDrag(PlayerCamera camera, string reason)
	{
		if (IsRemoteProxy(camera.dragItem))
		{
			if (PatchBridge.Impl?.CancelRemoteProxyDrag(camera, reason) != true)
			{
				ClearDrag(camera);
			}

			return;
		}

		ClearDrag(camera);
	}

	private static void ClearDrag(PlayerCamera camera)
	{
		camera.dragImage.enabled = false;
		camera.dragItem = null;
	}
}
