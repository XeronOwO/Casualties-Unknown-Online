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
/// Three outcomes are decided here rather than by the window: a proxy that cannot
/// be resolved is cancelled before the native body can mutate it (fail closed); a
/// LOCAL item released on another player's displayed backpack is a GIVE, decided
/// before the in-range USE the same release could otherwise be (the user's
/// 2026-10-22 ruling — the release's landing place decides, not the item); and a
/// local item released over an in-world remote player keeps the landed
/// cross-player use route.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleReleaseDragging")]
internal static class PlayerCameraDragUsePatch
{
	private static void Prefix(PlayerCamera __instance, List<RaycastResult> uiCasts, out ReleaseState __state)
	{
		__state = new ReleaseState { AutoOpened = RemoteBackpackView.AutoOpened };
		var dragItem = __instance.dragItem;
		if (dragItem == null) // Unity object — ==
		{
			return;
		}

		if (dragItem.GetComponent<RemoteCloneRender>() == null) // Unity object — ==
		{
			// The displayed backpack comes FIRST: a release on it is the GIVE, even when the cursor's world
			// point is also inside the player's detection range (where a release would be a USE). The player's
			// 2026-10-22 ruling separates the two outcomes by where the release lands, not by the item. The
			// target resolution is the ONE shared with the cue, so the label and the release cannot name
			// different players.
			if (PatchBridge.Impl is { } giveBridge)
			{
				var proxyTarget = DragGiveTargetResolver.Resolve(dragItem, __instance, uiCasts);
				if (proxyTarget is { } target)
				{
					// The answer says whether the intent actually went out: a refused give (a view-only pack, an
					// item with no authoritative id) must not keep the drag-opened backpack open.
					__state.Give = giveBridge.ReportLocalReleaseOntoProxy(dragItem, target.OwnerSteamId, target.Description, target.TargetItemInstanceId, target.TargetSlotIndex);
					ClearDrag(__instance);
					return;
				}
			}

			// A LOCAL item released over an in-world remote player is the landed
			// cross-player use-by-drag gesture: it consumes the release here,
			// before the native body can treat it as a local drop.
			if (PatchBridge.Impl?.TryHandleDraggedItemUseOnRemote(dragItem, __instance.body) == true)
			{
				ClearDrag(__instance);
				return;
			}

			return;
		}

		var itemId = RemoteDragProxyQuery.InstanceId(dragItem);
		var owner = RemoteDragProxyQuery.OwnerSteamId(dragItem);
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
		__state.ProxyWindow = true;
	}

	private static void Finalizer(ReleaseState __state)
	{
		if (__state.ProxyWindow)
		{
			PatchBridge.Impl?.EmitRemoteDragIntents(RemoteDragIntentWindow.Current.Close());
			return;
		}

		// The drag opened the displayed backpack by itself; a release that gave the item away keeps it (the
		// item's landing is what the giver is looking at), every other outcome closes it so the player's own
		// inventory comes back. A view the player opened from the player panel is not the drag's and stays.
		if (__state.AutoOpened && !__state.Give && RemoteBackpackView.IsOpen)
		{
			RemoteBackpackView.Close();
		}
	}

	/// <summary>
	/// The release's own state across the native body: whether the display-proxy intent window was opened
	/// (the finalizer must close and emit it), whether the view open at the release was the drag's own, and
	/// whether the release actually gave the item away (a give keeps that view).
	/// </summary>
	private struct ReleaseState
	{
		internal bool ProxyWindow;
		internal bool AutoOpened;
		internal bool Give;
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
