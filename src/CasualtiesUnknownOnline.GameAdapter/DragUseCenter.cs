using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The displayed backpack's centre is the USE/WEAR target (user ruling 2026-10-23). The hit test is the native
/// GEOMETRY — the guard the game's own <c>PlayerCamera.TryPerformRadialAction</c> applies to the pointer: the ring
/// has finished opening and the pointer is inside the centre circle. The native hit TAG cannot be the gate here:
/// CUO's remote view hides the tagged graphic (<c>RemoteBackpackView.AnchorRadialToFocus</c> disables
/// <c>radialCircle</c>), so a UI raycast can never reach it while a displayed backpack is open — the very context
/// this test exists for. The disabled component's transform and rect are still live, and the native centre TEXT
/// (<c>radialText</c>, "drag here to use") stays visible, which is the affordance the player reads.
/// </summary>
internal static class DragUseCenter
{
	internal static bool IsUnderPointer(PlayerCamera? camera)
	{
		if (camera == null) // Unity object — ==
		{
			return false;
		}

		if (camera.radialMenu == null || camera.radialCircle == null) // Unity objects — ==
		{
			return false;
		}

		if (camera.radialMenu.localScale.x <= PlayerCamera.inventoryUseLeniency || PlayerCamera.uiScale <= 0f)
		{
			return false;
		}

		var distance = Vector2.Distance(Input.mousePosition, camera.radialCircle.transform.position) / PlayerCamera.uiScale;
		return distance < camera.radialCircle.rectTransform.sizeDelta.x * 0.5f;
	}
}
