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
/// be resolved is cancelled before the native body can mutate it (fail closed), a
/// LOCAL item whose release target is another player's display proxy is cancelled
/// the same way (the reported item loss — see <c>ResolveDisplayProxyTarget</c>),
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
				return;
			}

			// The proxy-drag guard's mirror, on the TARGET side. While the remote
			// backpack view is open the ring and the container window render the
			// FOCUSED CLONE, so the item this release points at can be another
			// player's display proxy — and the native container branch would then
			// load THIS client's item into it (`container.LoadItem(this.dragItem)`,
			// PlayerCamera.cs:1568, and its R5 per-child loop at `:1590`), which is
			// the reported item loss: the child leaves its own bag, sits in the
			// proxy, and the clone rebuild unloads and destroys it. No intent can
			// name that move — the item is this client's own and the vocabulary
			// moves the OWNER's items — so the release fails closed here, before the
			// native body can mutate anything, and the item stays where it is.
			if (PatchBridge.Impl is { } bridge)
			{
				var proxyTarget = ResolveDisplayProxyTarget(__instance, uiCasts);
				if (proxyTarget is { } target)
				{
					bridge.ReportLocalReleaseOntoProxy(dragItem, target.OwnerSteamId, target.Description);
					ClearDrag(__instance);
				}
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
	/// The other side of a release this client makes: what the pointer resolves to,
	/// when that belongs to another player's displayed inventory. Both reads are the
	/// native release body's own — the first overlapping inventory button's item
	/// (the gate and the value <c>TryPerformInventoryAction</c> itself reads,
	/// <c>PlayerCamera.cs:1532</c>, <c>:1540</c>) and the container window's back
	/// panel, whose branch loads into <c>currentContainer</c> (<c>:1666</c>) — and a
	/// third for the one target that carries no item: a body slot of the displayed
	/// clone, whose index the native R8/R9 branch would run against the LOCAL body
	/// (<c>:1614-1631</c>), swapping or dropping items of this client's own body that
	/// the player never aimed at.
	///
	/// The ring is what makes those targets the clone's: <c>InvButtonBodyPatch</c>
	/// answers every button's <c>get_body</c> from the focused clone exactly while
	/// the view is open, so a body button is the displayed inventory's, and the
	/// container window's entries carry the proxy container's own children as their
	/// <c>refItem</c>.
	///
	/// The world fallback needs no read here: <c>CloneInventoryRenderer</c> disables
	/// every proxy collider ("never pickable/blocking"), so the release's own
	/// <c>Physics2D.OverlapPoint</c> (<c>:1702</c>) cannot reach one.
	/// </summary>
	private static DisplayProxyTarget? ResolveDisplayProxyTarget(PlayerCamera camera, List<RaycastResult>? uiCasts)
	{
		if (uiCasts is null)
		{
			return null;
		}

		foreach (var raycastResult in uiCasts)
		{
			var button = raycastResult.gameObject.GetComponent<InvButton>();
			if (button != null && button.Overlaps(uiCasts)) // Unity object — ==; the native gate (PlayerCamera.cs:1532)
			{
				var item = button.GetItem();
				if (item != null && RemoteDragProxyQuery.IsProxy(item)) // Unity objects — ==
				{
					return new DisplayProxyTarget(RemoteDragProxyQuery.OwnerSteamId(item), $"display proxy item {item.id}");
				}

				if (button.isBody && RemoteBackpackView.IsOpen)
				{
					// A body slot the ring reads from the clone: the item shown there
					// is a proxy (handled above), and an EMPTY one still names the
					// clone's slot index, which is what the native body would act on.
					return new DisplayProxyTarget(RemoteBackpackView.FocusedSteamId, "a slot of the displayed inventory");
				}
			}

			if (raycastResult.gameObject.CompareTag("ContainerBack")
				&& camera.currentContainer != null) // Unity object — ==; the native gate (PlayerCamera.cs:1666)
			{
				var containerItem = camera.currentContainer.GetComponent<Item>();
				if (containerItem != null && RemoteDragProxyQuery.IsProxy(containerItem)) // Unity objects — ==
				{
					return new DisplayProxyTarget(RemoteDragProxyQuery.OwnerSteamId(containerItem), $"display proxy container {containerItem.id}");
				}
			}
		}

		return null;
	}

	/// <summary>The player whose display proxy a local release pointed at, and what the pointer was on.</summary>
	private readonly record struct DisplayProxyTarget(ulong OwnerSteamId, string Description);

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
