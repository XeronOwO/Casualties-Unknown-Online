using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// KrokMP-style cross-player item use by drag: when the native drag release
/// happens over an in-world remote player, route the dragged usable item to the
/// existing cross-player use request and skip the native drop path. The
/// overlap check is world-space around the authoritative stream position (CUO
/// remote clones have no colliders).
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleReleaseDragging")]
internal static class PlayerCameraDragUsePatch
{
	private static bool Prefix(PlayerCamera __instance)
	{
		if (PatchBridge.Impl?.TryHandleDraggedItemUseOnRemote(__instance.dragItem, __instance.body) == true)
		{
			__instance.dragImage.enabled = false;
			__instance.dragItem = null;
			return false;
		}

		return true;
	}
}
