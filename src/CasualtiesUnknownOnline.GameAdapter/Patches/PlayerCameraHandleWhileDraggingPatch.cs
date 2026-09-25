using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The while-dragging frame of the remote backpack view. The game's own
/// <c>HandleWhileDragging</c> body now RUNS for the remote view: its hover
/// feedback, cursor, drag-image motion and radial state are the native ones, and
/// the continuous mutations it makes — the liquid drain tick, once per frame —
/// are captured by the while-dragging window instead of mutating a display proxy.
/// A proxy the window cannot bracket (no authoritative identity, or another item's
/// bracket) keeps the native body running for its feedback while the mutation seams
/// refuse the calls: the drain tick is skipped and reported
/// (<c>RemoteDragLiquidDrainPatch</c>), and the favourite write is covered below,
/// so no path mutates a proxy.
///
/// Two things stay CUO's, because that body reads the LOCAL scene. The radial menu
/// is anchored to the focused clone instead of the local body (the game anchors it
/// to the body, which is what made a remote view look broken). And the favourite
/// toggle — a direct <c>favourited</c> field write on the hovered item, which
/// Harmony has no call seam to intercept — is refused with one log line for the
/// frame it would happen, so a display proxy is never written; that frame's native
/// pass is skipped with it (the line says so), and the intent arrives with the stage
/// that builds that seam.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleWhileDragging")]
internal static class PlayerCameraHandleWhileDraggingPatch
{
	private static bool Prefix(PlayerCamera __instance, List<RaycastResult> uiCasts, out bool __state)
	{
		__state = false;
		if (!RemoteBackpackView.IsOpen || RemoteBackpackView.FocusedBody is not { } focused)
		{
			if (__instance.radialOpen)
			{
				__instance.radialCircle.enabled = true;
			}

			return true;
		}

		if (Camera.main == null) // Unity object — ==
		{
			// The native body dereferences the main camera (PlayerCamera.cs:1774);
			// a frame without one cannot run it.
			return false;
		}

		if (RefuseFavouriteOverAProxy(uiCasts))
		{
			return false;
		}

		OpenWhileDraggingWindow(__instance, out __state);
		AnchorRadialToTheFocusedClone(__instance, focused);
		return true;
	}

	private static void Finalizer(bool __state)
	{
		if (__state)
		{
			PatchBridge.Impl?.EmitRemoteWhileDraggingFrame(RemoteDragIntentWindow.Current.Close());
		}
	}

	/// <summary>
	/// The native favourite toggle (<c>PlayerCamera.cs:1745</c>) writes the HOVERED
	/// inventory button's item — not the dragged one — and it is a field store, so
	/// the window's call seams cannot take it. With the remote ring open that item is
	/// a display proxy, so the frame that would write it is skipped instead: the
	/// gesture is refused with one Information line and no proxy is mutated, the same
	/// rule every other intent a later stage carries follows. The hover test mirrors
	/// the native one (<c>:1736</c>): the first inventory button that overlaps the
	/// raycasts, and only when it carries an item.
	/// </summary>
	private static bool RefuseFavouriteOverAProxy(List<RaycastResult> uiCasts)
	{
		if (!Input.GetKeyDown(KeyBinds.GetBind("favourite")))
		{
			return false;
		}

		foreach (var raycastResult in uiCasts)
		{
			var button = raycastResult.gameObject.GetComponent<InvButton>();
			if (button == null || !button.Overlaps(uiCasts)) // Unity object — ==
			{
				continue;
			}

			var hovered = button.GetItem();
			if (hovered == null) // Unity object — ==
			{
				return false;
			}

			if (!RemoteDragProxyQuery.IsProxy(hovered))
			{
				return false;
			}

			PatchBridge.Impl?.ReportRemoteGestureNotCarried("favourite toggle through the remote view (this frame's native while-dragging pass is skipped with it, including that frame's drain tick)");
			return true;
		}

		return false;
	}

	/// <summary>
	/// Open the while-dragging bracket for this frame when the dragged item is a
	/// display proxy with an authoritative identity — the same resolution the release
	/// window uses, and for the same reason: a proxy without an identity can never
	/// name an item to replay, and its calls must still not run on the proxy. A LOCAL
	/// item dragged while the remote view is open is not bracketed: nothing it does
	/// would be a remote intent.
	/// </summary>
	private static void OpenWhileDraggingWindow(PlayerCamera camera, out bool state)
	{
		state = false;
		var dragItem = camera.dragItem;
		if (dragItem == null || dragItem.GetComponent<RemoteCloneRender>() == null) // Unity objects — ==
		{
			return;
		}

		var marker = dragItem.GetComponent<RemoteInventoryItemId>();
		var itemId = marker != null ? marker.Id : 0; // Unity object — ==
		var owner = marker != null && marker.OwnerSteamId != 0
			? marker.OwnerSteamId
			: RemoteBackpackView.FocusedSteamId;
		if (itemId == 0 || owner == 0)
		{
			return;
		}

		RemoteDragIntentWindow.Current.OpenWhileDragging(itemId, owner);
		state = true;
	}

	/// <summary>
	/// The game anchors the radial menu to the local body; with a remote backpack
	/// open the ring belongs to the focused clone, so the menu follows that clone's
	/// screen position and the use/wear circle stays hidden. The native body still
	/// runs and still colours that circle — only where it sits is CUO's.
	/// </summary>
	private static void AnchorRadialToTheFocusedClone(PlayerCamera camera, Body focused)
	{
		var screen = (Vector2)Camera.main.WorldToScreenPoint(focused.transform.position);
		RemoteBackpackView.UpdateSmoothPosition(screen);
		camera.radialMenu.transform.position = RemoteBackpackView.SmoothPosition;
		camera.radialCircle.enabled = false;
	}
}
