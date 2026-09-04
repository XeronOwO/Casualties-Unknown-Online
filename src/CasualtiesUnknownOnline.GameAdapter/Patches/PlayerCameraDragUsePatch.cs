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
		if (RemoteBackpackView.IsOpen)
		{
			if (PatchBridge.Impl?.TryHandleRemoteBackpackTake(__instance.dragItem) == true)
			{
				ClearDrag(__instance);
				return false;
			}

			// The remote backpack surface may only be consumed by the dedicated
			// host-authoritative operations below. Any other dragged item (a
			// world/local item picked up while the view is open) must be dropped
			// rather than allowed to mutate the display clone through the
			// original release path.
			if (IsRemoteProxy(__instance.dragItem))
			{
				if (TryHandleRemoteProxyRelease(__instance.dragItem, uiCasts))
				{
					ClearDrag(__instance);
					return false;
				}
			}

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
	/// Route one remote display-proxy release while the remote view is open.
	/// Container move has priority over edge gestures, then pour (water + left
	/// edge), then drop (left/right edge).
	/// </summary>
	private static bool TryHandleRemoteProxyRelease(Item dragItem, List<RaycastResult> uiCasts)
	{
		if (TryFindRemoteContainerTarget(uiCasts, out var target))
		{
			return PatchBridge.Impl?.TryHandleRemoteBackpackMoveToContainer(dragItem, target) == true;
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
