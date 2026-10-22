using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Patches;
using UnityEngine.EventSystems;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// One resolution of "what does this release point at in another player's displayed inventory": the give
/// target, shared by the release seam (which turns it into the intent) and the drag cue (which must hide its
/// USE label when the release would be a GIVE). One resolution, because a label and a release that resolve
/// the pointer separately can name different players — the defect this type exists to make impossible.
///
/// <para>
/// The reads are the native release body's own — the first overlapping inventory button's item (the gate and
/// the value <c>TryPerformInventoryAction</c> itself reads), and the container window's back panel, whose
/// branch loads into <c>currentContainer</c> — plus a third for the one target that carries no item: a body
/// slot of the displayed clone, whose index the native R8/R9 branch would otherwise run against the LOCAL
/// body. The ring is what makes those targets the clone's: <see cref="InvButtonBodyPatch"/> answers
/// every button's <c>get_body</c> from the focused clone exactly while the view is open.
/// </para>
///
/// <para>
/// The world fallback needs no read here: <c>CloneInventoryRenderer</c> disables every proxy collider
/// ("never pickable/blocking"), so the release's own <c>Physics2D.OverlapPoint</c> cannot reach one.
/// </para>
/// </summary>
internal static class DragGiveTargetResolver
{
	/// <summary>The give target the pointer is on, or null when the pointer is not on the displayed inventory.</summary>
	internal static DragGiveTarget? Resolve(Item? dragItem, PlayerCamera? camera, List<RaycastResult>? uiCasts)
	{
		if (dragItem == null || uiCasts is null || uiCasts.Count == 0) // Unity object — ==
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
					// What the pointer hit decides the OPERAND, and this is the only side that can tell: the
					// item shown here is a real object with (or without) a Container component, while the
					// host's snapshot of the other player carries no such flag. An item that really is a
					// container is named as the destination; anything else is not a container, so the release
					// means that RING SLOT — naming a container the host would then have to nest into a plain
					// item is what left the host's ledger stating a location the receiver never used.
					var container = item.GetComponent<Container>();
					return container != null // Unity object — ==
						? new DragGiveTarget(RemoteDragProxyQuery.OwnerSteamId(item), $"display proxy container item {item.id}", TargetItemInstanceId: RemoteDragProxyQuery.InstanceId(item))
						: new DragGiveTarget(RemoteDragProxyQuery.OwnerSteamId(item), $"display proxy item {item.id} in slot {button.slot}", TargetSlotIndex: button.slot);
				}

				if (button.isBody && RemoteBackpackView.IsOpen)
				{
					// A body slot the ring reads from the clone: the item shown there is a proxy (handled
					// above), and an EMPTY one still names the clone's slot index, which is what the native
					// body would act on.
					return new DragGiveTarget(RemoteBackpackView.FocusedSteamId, $"slot {button.slot} of the displayed inventory", TargetSlotIndex: button.slot);
				}
			}

			if (raycastResult.gameObject.CompareTag("ContainerBack"))
			{
				var openContainer = camera != null ? camera.currentContainer : null; // Unity object — ==
				if (openContainer != null) // Unity object — ==; the native gate (PlayerCamera.cs:1666)
				{
					var containerItem = openContainer!.GetComponent<Item>();
					if (containerItem != null && RemoteDragProxyQuery.IsProxy(containerItem)) // Unity objects — ==
					{
						return new DragGiveTarget(RemoteDragProxyQuery.OwnerSteamId(containerItem), $"display proxy container {containerItem.id}", TargetItemInstanceId: RemoteDragProxyQuery.InstanceId(containerItem));
					}
				}
			}
		}

		return null;
	}
}
