using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using HarmonyLib;
using UnityEngine.EventSystems;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The remote display-proxy release window. CUO no longer classifies the
/// gesture: the game's own <c>HandleReleaseDragging</c> body runs, and the
/// window is the call bracket around it — it opens here, the native mutation
/// entry points report the calls the native branch makes (see
/// <see cref="RemoteDragIntentWindow"/>), and the finalizer closes it and hands
/// the captured calls to the bridge as intents.
///
/// The bracket is a call bracket, not a time bracket: it spans exactly this one
/// invocation, so the only calls inside it are the ones the native body itself
/// makes. A projection rebuild (<c>CloneInventoryRenderer</c> loads and unloads
/// the container clones while it rebuilds the remote backpack), a packet pump or
/// any other Unity callback cannot be captured by construction — and the
/// finalizer closes the window even when the native body throws, so a failed
/// release cannot leak the window into the next frame.
///
/// Two outcomes are decided here rather than by the window: a proxy that cannot
/// be resolved is cancelled before the native body can mutate it (fail closed),
/// and a local item released over an in-world remote player keeps the landed
/// cross-player use route.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleReleaseDragging")]
internal static class PlayerCameraDragUsePatch
{
	private static void Prefix(PlayerCamera __instance, List<RaycastResult> uiCasts, out bool __state)
	{
		__state = false;
		var dragItem = __instance.dragItem;
		if (dragItem == null) // Unity object — ==
		{
			return;
		}

		if (dragItem.GetComponent<RemoteCloneRender>() == null) // Unity object — ==
		{
			// A LOCAL item released over an in-world remote player is the landed
			// cross-player use-by-drag gesture: it consumes the release here,
			// before the native body can treat it as a local drop.
			if (PatchBridge.Impl?.TryHandleDraggedItemUseOnRemote(dragItem, __instance.body) == true)
			{
				ClearDrag(__instance);
			}

			return;
		}

		var marker = dragItem.GetComponent<RemoteInventoryItemId>();
		var itemId = marker != null ? marker.Id : 0; // Unity object — ==
		var owner = marker != null && marker.OwnerSteamId != 0
			? marker.OwnerSteamId
			: RemoteBackpackView.FocusedSteamId;
		if (itemId == 0 || owner == 0)
		{
			// Fail closed: the native body would mutate the display proxy. This is
			// the case the deleted release-cancel policy guarded (the duplicate
			// water bottle: close the view while dragging, then release the held
			// proxy), and a proxy with no authoritative identity can never become
			// an intent.
			PatchBridge.Impl?.ReportRemoteDragUnresolved(dragItem);
			ClearDrag(__instance);
			return;
		}

		// While the remote view is open the native inventory ring shows the
		// focused clone (InvButtonBodyPatch), so a slot call names the owner's
		// body; once the view is closed the ring is the local body again and the
		// same call is the cross-player transfer onto the requester.
		RemoteDragIntentWindow.Current.Open(
			itemId,
			owner,
			PatchBridge.Impl?.LocalSteamId ?? 0,
			RemoteBackpackView.IsOpen,
			ClassifyNoOp(__instance, dragItem, uiCasts));
		__state = true;
	}

	private static void Finalizer(bool __state)
	{
		if (__state)
		{
			PatchBridge.Impl?.EmitRemoteDragIntents(RemoteDragIntentWindow.Current.Close());
		}
	}

	/// <summary>
	/// The native release outcomes that are deliberately nothing, so a release
	/// that produced no intent is reported as an unknown gesture only when it is
	/// none of these: R1 — the proxy released back onto its own inventory button
	/// (<c>PlayerCamera.cs:1536</c>); R7 — a wearable that cannot be held, where
	/// the native branch answers with its own alert (<c>:1609</c>); R14 — the
	/// craft button, local UI on the viewer (<c>:1673</c>).
	/// </summary>
	private static RemoteDragNoOp ClassifyNoOp(PlayerCamera camera, Item dragItem, List<RaycastResult>? uiCasts)
	{
		if (uiCasts is null)
		{
			return RemoteDragNoOp.None;
		}

		foreach (var raycastResult in uiCasts)
		{
			if (raycastResult.gameObject == camera.craftButton) // Unity object — ==
			{
				return RemoteDragNoOp.LocalUiOnly;
			}

			var button = raycastResult.gameObject.GetComponent<InvButton>();
			if (button == null || !button.Overlaps(uiCasts)) // Unity object — ==; the native gate (PlayerCamera.cs:1532)
			{
				continue;
			}

			if (button.GetItem() == dragItem) // Unity objects — ==
			{
				return RemoteDragNoOp.ReturnedToOwnSlot;
			}

			if (button.isBody && dragItem.Stats.wearable && !dragItem.Stats.wearableCanBeHeld)
			{
				return RemoteDragNoOp.WearableCannotBeHeld;
			}
		}

		return RemoteDragNoOp.None;
	}

	private static void ClearDrag(PlayerCamera camera)
	{
		if (camera.dragImage != null) // Unity object — ==
		{
			camera.dragImage.enabled = false;
		}

		camera.dragItem = null;
	}
}
