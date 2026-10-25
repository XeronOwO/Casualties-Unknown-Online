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
/// Three outcomes are decided here rather than by the window: a proxy that cannot be resolved is cancelled before
/// the native body can mutate it (fail closed); a LOCAL-item drag inside the player's OWN UI is left entirely to
/// the native local action (user ruling 2026-10-23 — the detection range is not read there); and, while another
/// player's backpack is DISPLAYED, a release on one of its slots is a GIVE (user ruling 2026-10-22 — the landing
/// place decides, not the item) while a release on its CENTRE is the USE/WEAR of the displayed player (the range no
/// longer decides a use). A release anywhere else keeps the native drop.
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
			// A drag inside the player's OWN UI is theirs alone (user ruling 2026-10-23): no other player's range
			// is read, so the release keeps the game's local action even when the cursor's world point happens to
			// sit inside somebody's detection range.
			var mode = GiveGestureModePolicy.Read(__instance);
			if (mode == GiveGestureMode.OwnUi)
			{
				return;
			}

			if (mode == GiveGestureMode.DisplayedBackpack)
			{
				// The displayed backpack comes FIRST: a release on it is the GIVE, even when the cursor's world
				// point is also inside the player's detection range (user ruling 2026-10-22). The target
				// resolution is the ONE shared with the cue, so the label and the release cannot name different
				// players.
				if (PatchBridge.Impl is { } giveBridge)
				{
					var proxyTarget = DragGiveTargetResolver.Resolve(dragItem, __instance, uiCasts);
					if (proxyTarget is { } target)
					{
						// The answer says whether the intent actually went out: a refused give (a view-only pack, an
						// item with no authoritative id) must not keep the drag-opened backpack open.
						__state.Give = giveBridge.ReportLocalReleaseOntoProxy(dragItem, target.OwnerSteamId, target.Description, target.TargetItemInstanceId, target.TargetSlotIndex);

						// The release's own click, which the skipped native body would have played: `HandleReleaseDragging` ends with
						// `this.PlayUISound(PlayerCamera.UISoundType.Click, 1f)` (`PlayerCamera.cs:1495`), and that tail is what the player hears when an
						// item lands in a slot. The give branch returns before the native body runs, so without this call the placement is silent — the
						// user's report ("dragging an item into a slot has no placement sound", 2026-10-25). It is the game's own call and clip, not a
						// stand-in.
						__instance.PlayUISound(PlayerCamera.UISoundType.Click, 1f);
						ClearDrag(__instance);
						return;
					}
				}

				// The displayed backpack's CENTRE is the use/wear target (user ruling 2026-10-23): the detection
				// range no longer decides a use. An item with no use/wear action is still consumed here — the
				// native centre would otherwise use or wear it on the GIVER.
				if (DragUseCenter.IsUnderPointer(__instance))
				{
					PatchBridge.Impl?.TryHandleDraggedItemUseOnRemote(dragItem, __instance.body);
					__state.Center = true;

					// The same release tail the native body would have run (see the give branch above): a consumed release keeps the click.
					__instance.PlayUISound(PlayerCamera.UISoundType.Click, 1f);
					ClearDrag(__instance);
					return;
				}

				// Anywhere else the game's own drop semantics run: the range only chose whose backpack is shown.
				return;
			}

			// World: no displayed backpack, so nothing of this family applies; the native release keeps the drop.
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

	private static void Finalizer(PlayerCamera __instance, ReleaseState __state)
	{
		if (__state.ProxyWindow)
		{
			PatchBridge.Impl?.EmitRemoteDragIntents(RemoteDragIntentWindow.Current.Close());
			return;
		}

		if (__state.Center)
		{
			// Keep the displayed backpack open after a centre use/wear (the vanilla single-player ring stays open).
			// The native body just ran its "release with no drag left" branch, which only keeps the ring while a UI
			// graphic is under the pointer — and the centre graphic is HIDDEN in the remote view, so it cleared
			// radialOpen. Re-assert the ring here, before the stale path can close the focus on the next frame.
			__instance.radialOpen = true;
			return;
		}

		// The drag opened the displayed backpack by itself; a release that gave the item away keeps it (the
		// item's landing is what the giver is looking at). Every other outcome closes it so the player's own
		// inventory comes back. A view the player opened from the player panel is not the drag's and stays.
		if (__state.AutoOpened && !__state.Give && RemoteBackpackView.IsOpen)
		{
			RemoteBackpackView.Close();
		}
	}

	/// <summary>
	/// The release's own state across the native body: whether the display-proxy intent window was opened
	/// (the finalizer must close and emit it), whether the view open at the release was the drag's own, whether the
	/// release actually gave the item away (a give keeps that view), and whether it was a centre use/wear (which
	/// keeps it too and re-asserts the native ring).
	/// </summary>
	private struct ReleaseState
	{
		internal bool ProxyWindow;
		internal bool AutoOpened;
		internal bool Give;
		internal bool Center;
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
