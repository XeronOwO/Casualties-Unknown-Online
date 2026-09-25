using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The while-dragging frame of the remote backpack view. The game's own
/// <c>HandleWhileDragging</c> body RUNS for the remote view: its hover
/// feedback, cursor, drag-image motion and radial state are the native ones, and
/// the mutations it makes — the liquid drain tick and the <c>favourited</c> field
/// store — are captured by the while-dragging window instead of mutating a display
/// proxy. A proxy the window cannot bracket (no authoritative identity, or another
/// item's bracket) keeps the native body running for its feedback while the
/// mutation seams refuse the calls: the drain tick is skipped and reported
/// (<c>RemoteDragLiquidDrainPatch</c>), and the favourite store is put back and
/// reported here, so no path mutates a proxy.
///
/// Two things stay CUO's, because that body reads the LOCAL scene. The radial menu
/// is anchored to the focused clone instead of the local body (the game anchors it
/// to the body, which is what made a remote view look broken). And the favourite
/// toggle is the one mutation the game expresses as a FIELD write with no call
/// behind it (<c>PlayerCamera.cs:1747</c>), so this patch reads the frame's own
/// candidate buttons before the native body and compares them after: the native
/// condition still decides whether the store happens, and CUO only turns the store
/// it observes into an intent for the owner.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleWhileDragging")]
internal static class PlayerCameraHandleWhileDraggingPatch
{
	private static bool Prefix(PlayerCamera __instance, List<RaycastResult> uiCasts, out FavouriteFrameState __state)
	{
		__state = SnapshotFavourites(uiCasts);
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

		__state.BracketOpened = OpenWhileDraggingWindow(__instance);
		AnchorRadialToTheFocusedClone(__instance, focused);
		return true;
	}

	/// <summary>
	/// The frame's own mutations become intents here, BEFORE the bracket closes. A
	/// finalizer runs even when the native body threw, so a field store the body
	/// already made is never left on a display proxy and never silently lost — which
	/// is why the comparison is not a postfix.
	/// </summary>
	private static void Finalizer(FavouriteFrameState __state)
	{
		CaptureFavouriteStores(__state);
		if (__state.BracketOpened)
		{
			PatchBridge.Impl?.EmitRemoteWhileDraggingFrame(RemoteDragIntentWindow.Current.Close());
		}
	}

	/// <summary>
	/// The items of the frame's overlapping inventory buttons, with the
	/// <c>favourited</c> value each one carries before the native body runs. The
	/// native store writes the item of the first inventory button that overlaps the
	/// raycasts (<c>PlayerCamera.cs:1736-1747</c>), so those buttons are exactly the
	/// candidates; the same button reached twice by the raycast list is one candidate.
	/// </summary>
	private static FavouriteFrameState SnapshotFavourites(List<RaycastResult> uiCasts)
	{
		var candidates = new List<FavouriteCandidate>();
		if (!RemoteBackpackView.IsOpen)
		{
			return new FavouriteFrameState(candidates);
		}

		foreach (var raycastResult in uiCasts)
		{
			var button = raycastResult.gameObject.GetComponent<InvButton>();
			if (button == null || !button.Overlaps(uiCasts)) // Unity object — ==; the native gate (PlayerCamera.cs:1736)
			{
				continue;
			}

			var item = button.GetItem();
			if (item == null || Contains(candidates, item)) // Unity object — ==
			{
				continue;
			}

			candidates.Add(new FavouriteCandidate(item, item.favourited));
		}

		return new FavouriteFrameState(candidates);
	}

	/// <summary>
	/// The frame's favourite stores. A store on a display proxy becomes an intent for
	/// the owner and the proxy's field goes back to the value the frame started with —
	/// no path may mutate a proxy, and the projection carries the owner's authoritative
	/// value back. A proxy no while-dragging bracket took is refused with one line,
	/// exactly like the drain seam. A store on a LOCAL item is left alone: that is the
	/// native local gesture, unchanged by the remote view being open.
	/// </summary>
	private static void CaptureFavouriteStores(FavouriteFrameState state)
	{
		foreach (var candidate in state.Candidates)
		{
			var item = candidate.Item;
			if (item == null || item.favourited == candidate.Favourited) // Unity object — ==
			{
				continue;
			}

			if (!RemoteDragProxyQuery.IsProxy(item))
			{
				continue;
			}

			item.favourited = candidate.Favourited;
			var itemId = RemoteDragProxyQuery.InstanceId(item);
			if (itemId == 0 || !RemoteDragIntentWindow.Current.IsOpen)
			{
				PatchBridge.Impl?.ReportProxyNotOperable(item, "the favourite toggle");
				continue;
			}

			RemoteDragIntentWindow.Current.CaptureFavourite(itemId, RemoteDragProxyQuery.OwnerSteamId(item));
		}
	}

	private static bool Contains(List<FavouriteCandidate> candidates, Item item)
	{
		foreach (var candidate in candidates)
		{
			if (candidate.Item == item) // Unity object — ==
			{
				return true;
			}
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
	private static bool OpenWhileDraggingWindow(PlayerCamera camera)
	{
		var dragItem = camera.dragItem;
		if (dragItem == null || dragItem.GetComponent<RemoteCloneRender>() == null) // Unity objects — ==
		{
			return false;
		}

		var itemId = RemoteDragProxyQuery.InstanceId(dragItem);
		var owner = RemoteDragProxyQuery.OwnerSteamId(dragItem);
		if (itemId == 0 || owner == 0)
		{
			return false;
		}

		RemoteDragIntentWindow.Current.OpenWhileDragging(itemId, owner);
		return true;
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

	/// <summary>One inventory button's item and the <c>favourited</c> value it carried before the frame ran.</summary>
	private readonly record struct FavouriteCandidate(Item Item, bool Favourited);

	/// <summary>The frame's favourite candidates, plus whether this patch opened the frame's bracket.</summary>
	private struct FavouriteFrameState(List<FavouriteCandidate> candidates)
	{
		internal List<FavouriteCandidate> Candidates { get; } = candidates;

		internal bool BracketOpened { get; set; }
	}
}
