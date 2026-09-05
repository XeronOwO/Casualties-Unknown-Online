using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Keeps the native radial inventory attached to the focused remote clone and
/// shows that player's name while the remote backpack view is open. The game
/// otherwise anchors the radial menu to the local body (PlayerCamera.cs:1923),
/// which is exactly what makes a remote-inventory view look broken.
/// This is now a prefix that also skips the original while-dragging body for
/// the remote view: the original contains native inventory mutations (favourite
/// toggles on the hovered item), which must never run against a display proxy.
/// Remote take is handled on release by <see cref="PlayerCameraDragUsePatch"/>.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleWhileDragging")]
internal static class PlayerCameraHandleWhileDraggingPatch
{
	private static bool Prefix(PlayerCamera __instance, List<RaycastResult> uiCasts)
	{
		if (!RemoteBackpackView.IsOpen || RemoteBackpackView.FocusedBody is not { } focused)
		{
			if (__instance.radialOpen)
			{
				__instance.radialCircle.enabled = true;
			}

			return true;
		}

		if (Input.GetKeyDown(KeyBinds.GetBind("favourite")))
		{
			TryToggleFavoriteOnHoveredRemoteItem(uiCasts);
		}

		if (Camera.main == null) // Unity object — ==
		{
			return false;
		}

		var screen = (Vector2)Camera.main.WorldToScreenPoint(focused.transform.position);
		RemoteBackpackView.UpdateSmoothPosition(screen);
		__instance.radialMenu.transform.position = RemoteBackpackView.SmoothPosition;
		__instance.radialCircle.enabled = false;

		// Keep the dragged image following the mouse so the remote-take drag
		// still feels native before the release sends the host request.
		if (__instance.dragItem != null) // Unity object — ==
		{
			__instance.dragImage.rectTransform.position = Input.mousePosition;
		}

		return false;
	}

	private static void TryToggleFavoriteOnHoveredRemoteItem(List<RaycastResult> uiCasts)
	{
		foreach (var raycastResult in uiCasts)
		{
			var button = raycastResult.gameObject.GetComponent<InvButton>();
			if (button == null || !button.Overlaps(uiCasts)) // Unity object — ==
			{
				continue;
			}

			var item = button.GetItem();
			if (item != null && item.GetComponent<RemoteCloneRender>() != null) // Unity objects — ==
			{
				PatchBridge.Impl?.TryHandleRemoteBackpackFavoriteToggle(item);
				return;
			}
		}
	}
}
