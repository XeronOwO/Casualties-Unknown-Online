using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Keeps the native radial inventory attached to the focused remote clone and
/// shows that player's name while the remote backpack view is open. The game
/// otherwise anchors the radial menu to the local body (PlayerCamera.cs:1923),
/// which is exactly what makes a remote-inventory view look broken.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleWhileDragging")]
internal static class PlayerCameraHandleWhileDraggingPatch
{
	private static void Postfix(PlayerCamera __instance)
	{
		if (!RemoteBackpackView.IsOpen || RemoteBackpackView.FocusedBody is not { } focused)
		{
			if (__instance.radialOpen)
			{
				__instance.radialCircle.enabled = true;
			}

			return;
		}

		if (Camera.main == null) // Unity object — ==
		{
			return;
		}

		var screen = (Vector2)Camera.main.WorldToScreenPoint(focused.transform.position);
		RemoteBackpackView.UpdateSmoothPosition(screen);
		__instance.radialMenu.transform.position = RemoteBackpackView.SmoothPosition;
		__instance.radialCircle.enabled = false;
	}
}
