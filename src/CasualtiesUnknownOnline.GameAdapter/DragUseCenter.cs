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
	/// <summary>
	/// The pointer this guard reads, when an acceptance batch has staged one — <c>null</c> for every real player, where the game's own
	/// <c>Input.mousePosition</c> is the answer. Set by the driver recipe for the length of one release and cleared immediately after.
	/// </summary>
	internal static Vector2? StagedPointer { get; private set; }

	/// <summary>Stages (or clears, with <c>null</c>) the pointer this guard reads. Test-only entry: nothing in the product calls it.</summary>
	internal static void StagePointer(Vector2? pointer) => StagedPointer = pointer;
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

		// The pointer comes from ONE place, so an acceptance batch can stage the position the guard reads without OS-level input:
		// `StagePointer` is set by the driver's own recipe for the length of ONE release and cleared immediately after, and the real pointer is the
		// default for every player. Without this seam "drag onto the displayed backpack's centre" was the only gesture family left that a batch could
		// not drive (the guard read `Input.mousePosition` directly, so no staged `clickPos` could satisfy it — measured 2026-10-26).
		var pointer = StagedPointer ?? (Vector2)Input.mousePosition;
		var distance = Vector2.Distance(pointer, camera.radialCircle.transform.position) / PlayerCamera.uiScale;
		return distance < camera.radialCircle.rectTransform.sizeDelta.x * 0.5f;
	}
}
