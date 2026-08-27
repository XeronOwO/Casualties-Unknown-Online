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
				__instance.dragImage.enabled = false;
				__instance.dragItem = null;
				return false;
			}

			// The remote backpack surface is read-only except for the take
			// operation above. Any other dragged item (a world/local item picked
			// up while the view is open) must be dropped rather than allowed to
			// mutate the display clone through the original release path.
			if (__instance.dragItem != null) // Unity object — ==
			{
				__instance.dragImage.enabled = false;
				__instance.dragItem = null;
				return false;
			}

			return true;
		}

		if (PatchBridge.Impl?.TryHandleDraggedItemUseOnRemote(__instance.dragItem, __instance.body) == true)
		{
			__instance.dragImage.enabled = false;
			__instance.dragItem = null;
			return false;
		}

		return true;
	}
}
