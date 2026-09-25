using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Keeps the native radial inventory attached to the focused remote clone and
/// shows that player's name while the remote backpack view is open. The game
/// otherwise anchors the radial menu to the local body (PlayerCamera.cs:1923),
/// which is exactly what makes a remote-inventory view look broken.
///
/// This is still a prefix that skips the original while-dragging body for the
/// remote view: that body contains native mutations against the HOVERED item
/// (the favourite toggle) and the continuous liquid drain, and the hovered item
/// here is a display proxy. Both are reported when the gesture is actually made,
/// so a player who presses the key or holds a water container over the drain gets
/// a line instead of a silent nothing; their intents arrive with the stages that
/// restore this body.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleWhileDragging")]
internal static class PlayerCameraHandleWhileDraggingPatch
{
	/// <summary>The proxy the drain refusal was last reported for — one line per drag, not one per frame.</summary>
	private static ulong _drainReportedItemId;

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
			PatchBridge.Impl?.ReportRemoteGestureNotCarried("favourite toggle through the remote view");
		}

		ReportDrainOnce(__instance, uiCasts);

		if (Camera.main == null) // Unity object — ==
		{
			return false;
		}

		var screen = (Vector2)Camera.main.WorldToScreenPoint(focused.transform.position);
		RemoteBackpackView.UpdateSmoothPosition(screen);
		__instance.radialMenu.transform.position = RemoteBackpackView.SmoothPosition;
		__instance.radialCircle.enabled = false;

		// Keep the dragged image following the mouse so the drag still feels
		// native before the release produces its intent.
		if (__instance.dragItem != null) // Unity object — ==
		{
			__instance.dragImage.rectTransform.position = Input.mousePosition;
		}

		return false;
	}

	/// <summary>
	/// The native drain tick (PlayerCamera.cs:1729) runs every frame while a water
	/// container is dragged over the drain object; the drain object is only active
	/// for a water container that still holds liquid (:1896), so hovering it is
	/// the gesture. One report per dragged proxy.
	/// </summary>
	private static void ReportDrainOnce(PlayerCamera camera, List<RaycastResult> uiCasts)
	{
		if (camera.liquidDrainObject == null || camera.dragItem == null) // Unity objects — ==
		{
			return;
		}

		var itemId = RemoteDragProxyQuery.InstanceId(camera.dragItem);
		if (itemId == 0 || itemId == _drainReportedItemId)
		{
			return;
		}

		foreach (var raycastResult in uiCasts)
		{
			if (raycastResult.gameObject != camera.liquidDrainObject) // Unity object — ==
			{
				continue;
			}

			_drainReportedItemId = itemId;
			PatchBridge.Impl?.ReportRemoteGestureNotCarried("the liquid drain tick through the remote view");
			return;
		}
	}
}
