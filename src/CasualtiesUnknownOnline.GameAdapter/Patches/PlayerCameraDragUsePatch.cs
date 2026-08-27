using CasualtiesUnknownOnline.GameAdapter.Character;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Cross-player native drag release. Two operations share this seam:
/// (1) remote-backpack take — while the native remote backpack view is open,
/// dragging a display-proxy item out is a host-authoritative take request, never
/// a local body mutation; (2) KrokMP-style cross-player item use by drag — when
/// the native drag release happens over an in-world remote player, route the
/// dragged usable item to the existing cross-player use request and skip the
/// native drop path. Remote clones have no colliders, so overlap is world-space
/// around the authoritative stream position.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleReleaseDragging")]
internal static class PlayerCameraDragUsePatch
{
	private static bool Prefix(PlayerCamera __instance)
	{
		if (RemoteBackpackView.IsOpen)
		{
			if (PatchBridge.Impl?.TryHandleRemoteBackpackTake(__instance.dragItem) == true)
			{
				ClearDrag(__instance);
				return false;
			}

			// The remote backpack surface is read-only except for the take
			// operation above. Any other dragged item (a world/local item picked
			// up while the view is open) must be dropped rather than allowed to
			// mutate the display clone through the original release path.
			if (__instance.dragItem != null) // Unity object — ==
			{
				CancelDrag(__instance, "remote backpack view did not consume the drag");
				return false;
			}

			return true;
		}

		// A display proxy picked up from the remote view is the only drag that
		// can legally outlive that view. It may ONLY be consumed by the
		// remote-take path; if the view is closed (or the take did not happen)
		// the proxy must be cancelled before the original native release or the
		// cross-player use path can move it into an authoritative inventory.
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
