using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Content;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The remote clones' inventory rendering: each slot of a clone shows the
/// carried item's prefab, and each worn item (negative SlotIndex — limb-encoded)
/// renders on the matching limb (mouth/hat/back…, the game parents them there
/// too, Body.cs:1508). Pure display — physics off, non-interactive, no instance
/// id. Every render is re-created from the owner's character snapshot: matching
/// items stay (their component state is refreshed — the 1 Hz rebuild used to
/// recreate every clone every second and strobe the light), changed ones swap,
/// the emptied disappear. Called by the clone renderer when a clone appears and
/// when a snapshot updates. Split out of CharacterDataSync when the 600-line
/// gate demanded it — the data domain stays there, the display domain here.
/// </summary>
internal sealed class CloneInventoryRenderer(ILogger<CloneInventoryRenderer> log)
{
	private readonly ILogger<CloneInventoryRenderer> _log = log;

	internal void ApplyCloneInventory(Body clone, CharacterDataMsg data, ulong ownerSteamId)
	{
		_log.LogDebug("[CloneRender] apply {Count} items to clone slots ({Slots} slots).", data.Items.Count, clone.slots.Length);
		foreach (var slot in clone.slots)
		{
			if (slot == null) // Unity object — ==
			{
				continue;
			}

			var wanted = data.Items.FirstOrDefault(x => x.SlotIndex == slot.slot);
			RenderItemInto(slot.transform, wanted, slot.spriteSortOrder, wearLimb: null, ownerSteamId);
		}

		for (var i = 0; i < clone.limbs.Length; i++)
		{
			var limb = clone.limbs[i];
			if (limb == null) // Unity object — ==
			{
				continue;
			}

			var worn = data.Items.FirstOrDefault(x => x.SlotIndex == -(i + 2));
			RenderItemInto(limb.transform, worn, 0, wearLimb: limb, ownerSteamId);
		}
	}

	/// <summary>
	/// Materialize one snapshot item into a render parent. Slot parents are
	/// fully cleared (a slot only ever holds items); limb parents keep the
	/// game's own children (bones/decorations) and clear only our previous
	/// renders (RemoteCloneRender-marked).
	/// </summary>
	private static void RenderItemInto(Transform parent, CharacterItemMsg? wanted, int sortOrder, Limb? wearLimb, ulong ownerSteamId)
	{
		// A matching render stays — the 1 Hz rebuild used to recreate every clone
		// every second: a freshly Instantiated clone starts at the prefab's
		// default orientation and gets yanked toward the mouse on the next frame
		// (CustomItemBehaviour points hand-slot items at the local mouse), which
		// reads as a strobe — the light's visual position jumps once per second.
		// Keeping the matching clone removes the per-second jump entirely; its
		// component state is still refreshed from the snapshot below, so the
		// flashlight mode follows ≤1 s behind. Limb parents: only match renders
		// we own (RemoteCloneRender), never the game's own children.
		if (wanted != null)
		{
			// Slots and limbs contain only their own carried/worn item. Matching
			// only direct children prevents a nested container child with the
			// same item id from being mistaken for the slot/worn item and
			// destroyed as a "stray"; on limbs we additionally require the CUO
			// render marker so the game's own limb children are never matched.
			var matches = wearLimb == null
				? FindDirectSlotMatches(parent, wanted.ItemId)
				: FindDirectLimbMatches(parent, wanted.ItemId);
			if (matches.Length > 0)
			{
				// Keep the first; destroy any further copies — the reason the
				// old diff (GetChild(0)-only) was abandoned was stray duplicates
				// accumulating in a slot; the incremental path must not resurrect
				// them.
				for (var i = 1; i < matches.Length; i++)
				{
					var duplicate = matches[i];
					if (duplicate.GetComponent<Container>() != null) // Unity object — ==
					{
						RemoteBackpackView.NotifyOpenContainerProxyRemoved(duplicate);
					}

					Object.Destroy(duplicate.gameObject);
				}

				if (wanted.Components is { Count: > 0 })
				{
					ItemStateCodec.RestoreComponentStates(matches[0], wanted.Components);
				}

				RemoteItemPresentation.Apply(matches[0], wanted);
				if (matches[0].GetComponent<RemoteCloneRender>() == null) // Unity object — ==
				{
					matches[0].gameObject.AddComponent<RemoteCloneRender>();
				}

				SetRemoteInventoryItemId(matches[0], wanted.InstanceId, ownerSteamId);
				RestoreRemoteContents(matches[0], wanted.Contents, ownerSteamId);
				return;
			}
		}

		if (wearLimb == null)
		{
			// Clear EVERY child, then materialize the wanted item: the diff
			// used to inspect only GetChild(0), so a slot that accumulated
			// more than one child (template leftover + render, or repeated
			// renders) kept the strays — peers saw duplicate carried items
			// appear after inventory shuffling.
			for (var c = parent.childCount - 1; c >= 0; c--)
			{
				var child = parent.GetChild(c);
				var childItem = child.GetComponent<Item>();
				if (childItem != null && childItem.GetComponent<Container>() != null) // Unity objects — ==
				{
					RemoteBackpackView.NotifyOpenContainerProxyRemoved(childItem);
				}

				Object.Destroy(child.gameObject);
			}
		}
		else
		{
			for (var c = parent.childCount - 1; c >= 0; c--)
			{
				var child = parent.GetChild(c);
				if (child.GetComponent<RemoteCloneRender>() != null) // Unity object — ==
				{
					var childItem = child.GetComponent<Item>();
					if (childItem != null && childItem.GetComponent<Container>() != null) // Unity objects — ==
					{
						RemoteBackpackView.NotifyOpenContainerProxyRemoved(childItem);
					}

					Object.Destroy(child.gameObject);
				}
			}
		}

		if (wanted is null)
		{
			return;
		}

		var prefab = ItemPrefabResolver.Load(wanted.ItemId);
		if (prefab == null) // Unity object — ==
		{
			return;
		}

		var obj = Object.Instantiate(prefab, parent) as GameObject;
		if (obj == null) // Unity object — ==
		{
			return;
		}

		obj.SetActive(true);
		obj.transform.localPosition = Vector3.zero;
		var item = obj.GetComponent<Item>();

		// Apply the snapshot's component state so the clone shows the owner's
		// real state (CustomItemBehaviour.state — flashlight modes). The prefab
		// Light2D starts with its author-time light: set its enabled to what the
		// restored state says BEFORE the first Update — a rebuilt clone then
		// never shows one wrong frame (the 1 Hz rebuild used to strobe the light
		// when the owner was off; forcing it off strobed a black frame when the
		// owner was on). LightItem/CustomItemBehaviour drive from next frame on
		// (enabled = shouldEnable && !inContainer, intensity = state).
		if (wanted.Components is { Count: > 0 })
		{
			ItemStateCodec.RestoreComponentStates(item, wanted.Components);
		}

		// Owner-local component state also needs display-only presentation on
		// the clone (GrapplingHook sprite/script isolation, Watch/AutoPump
		// script isolation) — a hidden NRE after restoring the fired flag is
		// exactly the class of bug this call prevents.
		RemoteItemPresentation.Apply(item, wanted);

		var modeState = item.GetComponent<CustomItemBehaviour>() != null
			? item.GetComponent<CustomItemBehaviour>().state
			: 0;
		// Light2D lives in the URP runtime assembly (not referenced here) —
		// matched by name, same convention as RestoreComponentStates.
		foreach (var light in obj.GetComponentsInChildren<Behaviour>(true))
		{
			if (light.GetType().Name == "Light2D")
			{
				light.enabled = modeState != 0;
			}
		}


		obj.transform.localEulerAngles = wearLimb != null
			? Vector3.zero // the game wears with identity rotation (Body.cs:1510)
			: new Vector3(0f, 0f, item.Stats.slotRotation);
		if (item.rb != null) // Unity object — ==
		{
			item.rb.simulated = false; // pure display
		}

		var col = obj.GetComponent<Collider2D>();
		if (col != null) // Unity object — ==
		{
			col.enabled = false; // never pickable/blocking
		}

		var sr = obj.GetComponent<SpriteRenderer>();
		if (sr != null) // Unity object — ==
		{
			// Wear order mirrors the game (Body.cs:1507): limb sprite order +
			// the item's wearable visual offset.
			sr.sortingOrder = wearLimb != null
				? wearLimb.GetComponent<SpriteRenderer>().sortingOrder + item.Stats.wearableVisualOffset
				: sortOrder;
		}

		// Remote worn items are display proxies parented directly to the clone
		// limb; apply the custom worn sprite/offset here because the clone path
		// never runs the vanilla WearWearable flow.
		if (wearLimb != null)
		{
			item.GetComponent<CustomItemVisualState>()?.ApplyWornVisual();
			item.GetComponent<CustomItemVisualState>()?.EnsureSecondarySprites(wearLimb.GetComponentInParent<Body>());
		}

		// Every clone render gets the marker now: limb parents still use it to
		// clear only our renders, and the uniform marker lets presentation code
		// identify a display-proxy item without depending on slot internals.
		obj.AddComponent<RemoteCloneRender>();
		SetRemoteInventoryItemId(item, wanted.InstanceId, ownerSteamId);
		RestoreRemoteContents(item, wanted.Contents, ownerSteamId);
	}

	private static Item[] FindDirectSlotMatches(Transform parent, string itemId)
	{
		var matches = new List<Item>();
		for (var i = 0; i < parent.childCount; i++)
		{
			var child = parent.GetChild(i).GetComponent<Item>();
			if (child != null && child.id == itemId) // Unity object — ==
			{
				matches.Add(child);
			}
		}

		return [.. matches];
	}

	private static Item[] FindDirectLimbMatches(Transform parent, string itemId)
	{
		var matches = new List<Item>();
		for (var i = 0; i < parent.childCount; i++)
		{
			var child = parent.GetChild(i);
			var item = child.GetComponent<Item>();
			if (item != null
				&& item.id == itemId
				&& item.GetComponent<RemoteCloneRender>() != null) // Unity objects — ==
			{
				matches.Add(item);
			}
		}

		return [.. matches];
	}

	/// <summary>
	/// Rebuilds a remote clone container's child items from the snapshot's
	/// recursive contents. The native container/backpack UI reads a real
	/// <see cref="Container"/> transform, so a remote container that has
	/// contents must materialise those children on the display clone; they are
	/// marked as remote render proxies (no authority, no physics).
	/// </summary>
	private static void RestoreRemoteContents(Item containerItem, List<CharacterItemMsg> contents, ulong ownerSteamId)
	{
		if (containerItem == null) // Unity object — ==
		{
			return;
		}

		var container = containerItem.GetComponent<Container>();
		var previous = new List<Item>();
		if (container != null) // Unity object — ==
		{
			// Only direct children of this container are this level's contents.
			// A recursive GetComponentsInChildren scan would also see proxies
			// inside nested containers and could match a child to the wrong
			// depth (especially for id=0/unbound items).
			for (var i = 0; i < container.transform.childCount; i++)
			{
				var child = container.transform.GetChild(i).GetComponent<Item>();
				if (child != null && child.GetComponent<RemoteCloneRender>() != null) // Unity objects — ==
				{
					previous.Add(child);
				}
			}
		}

		var used = new List<Item>();
		var structureChanged = false;

		if (container != null) // Unity object — ==
		{
			// Incremental reconciliation: keep an existing proxy when the same
			// authoritative instance id is still present, so an open native
			// container window (whose buttons hold direct Item references) never
			// loses the item it is displaying and a user dragging a container
			// child is not destroyed by a periodic snapshot. Only removed/new
			// children are destroyed/created, and those are unloaded from the
			// container immediately to avoid the one-frame double weight.
			foreach (var childData in contents)
			{
				var match = FindExistingProxy(previous, used, childData);
				if (match != null) // Unity object — ==
				{
					used.Add(match);
					UpdateRemoteContent(match, childData, ownerSteamId);
					continue;
				}

				RestoreRemoteContent(containerItem, container, childData, ownerSteamId);
				structureChanged = true;
			}
		}

		foreach (var old in previous)
		{
			if (used.Contains(old)) // Unity object — == list calls operator overload
			{
				continue;
			}

			if (container != null && old.transform.parent == container.transform) // Unity objects — ==
			{
				container.UnloadItem(old);
			}

			// A removed child may be the container the native window is showing
			// (nested container re-homed/replaced) — let the open window re-bind
			// on the next frame before it disappears from the UI.
			RemoteBackpackView.NotifyOpenContainerProxyRemoved(old);

			// UnloadItem re-enables the proxy's rigidbody/sprite as part of the
			// native container contract; this item is being removed, so hide it
			// immediately rather than letting it appear as a ghost for the rest
			// of the frame.
			old.gameObject.SetActive(false);
			Object.Destroy(old.gameObject);
			structureChanged = true;
		}

		MarkRemoteCloneTree(containerItem);
		if (structureChanged)
		{
			RefreshOpenRemoteContainer(container);
		}
	}

	private static Item? FindExistingProxy(
		IReadOnlyList<Item> previous,
		List<Item> used,
		CharacterItemMsg childData)
	{
		foreach (var candidate in previous)
		{
			if (used.Contains(candidate)) // Unity object — ==
			{
				continue;
			}

			var marker = candidate.GetComponent<RemoteInventoryItemId>();
			if (childData.InstanceId != 0)
			{
				// The authoritative id is the stable identity; a proxy marker
				// carries exactly that id. Unbound items (id 0) are matched by
				// item id below only when no marker has been assigned yet.
				if (marker != null && marker.Id == childData.InstanceId) // Unity object — ==
				{
					return candidate;
				}
			}
			else if (marker == null && candidate.id == childData.ItemId) // Unity objects — ==
			{
				return candidate;
			}
		}

		return null;
	}

	private static void UpdateRemoteContent(Item item, CharacterItemMsg data, ulong ownerSteamId)
	{
		SetRemoteInventoryItemId(item, data.InstanceId, ownerSteamId);
		item.condition = data.Condition;
		item.favourited = data.Favourited;
		ItemStateCodec.RestoreLiquids(item, data.Liquids);
		ItemStateCodec.RestoreComponentStates(item, data.Components);
		RemoteItemPresentation.Apply(item, data);
		RestoreRemoteContents(item, data.Contents, ownerSteamId);
	}

	private static void RefreshOpenRemoteContainer(Container? container)
	{
		if (container == null || !RemoteBackpackView.IsOpen || PlayerCamera.main == null) // Unity objects — ==
		{
			return;
		}

		// The native container window is populated once on open and does not
		// auto-refresh when the remote clone's container children are re-rendered
		// asynchronously. Without this, the open window keeps buttons to the old
		// destroyed proxy items (visible → invisible after a snapshot) until the
		// user closes/reopens. Repopulating is deferred one frame so the next
		// frame sees the old proxy children already destroyed.
		if (PlayerCamera.main.currentContainer != container) // Unity objects — ==
		{
			return;
		}

		RemoteBackpackView.RequestOpenContainerRefresh();
	}

	private static void RestoreRemoteContent(Item containerItem, Container container, CharacterItemMsg childData, ulong ownerSteamId)
	{
		var prefab = ItemPrefabResolver.Load(childData.ItemId);
		if (prefab == null) // Unity object — ==
		{
			return;
		}

		var go = Object.Instantiate(prefab, containerItem.transform.position, Quaternion.identity);
		go.SetActive(true);
		var child = go.GetComponent<Item>();
		if (child == null) // Unity object — ==
		{
			Object.Destroy(go);
			return;
		}

		child.condition = childData.Condition;
		child.favourited = childData.Favourited;
		SetRemoteInventoryItemId(child, childData.InstanceId, ownerSteamId);
		ItemStateCodec.RestoreLiquids(child, childData.Liquids);
		ItemStateCodec.RestoreComponentStates(child, childData.Components);

		var childContainer = child.GetComponent<Container>();
		if (childContainer != null && childData.Contents.Count > 0) // Unity object — ==
		{
			// Native Container.LoadItem refuses a container that already holds
			// items (stacking guard), but remote display proxies must be able to
			// represent a nested container with contents. Attach it manually with
			// the same presentation contract and let RestoreRemoteContents fill
			// the tree while it is already parented.
			child.transform.SetParent(container.transform);
			child.transform.localPosition = Vector3.zero;
			child.transform.localEulerAngles = Vector3.zero;
			if (child.rb != null) // Unity object — ==
			{
				child.rb.simulated = false;
			}

			var sr = child.GetComponent<SpriteRenderer>();
			if (sr != null) // Unity object — ==
			{
				sr.enabled = container.itemsVisible;
			}

			Container.UpdateItemLight(child.gameObject, !container.itemsVisible);
			RestoreRemoteContents(child, childData.Contents, ownerSteamId);
			return;
		}

		RestoreRemoteContents(child, childData.Contents, ownerSteamId);
		container.LoadItem(child);
	}

	private static void SetRemoteInventoryItemId(Item item, ulong instanceId, ulong ownerSteamId)
	{
		if (instanceId == 0)
		{
			return;
		}

		var marker = item.GetComponent<RemoteInventoryItemId>();
		if (marker == null) // Unity object — ==
		{
			marker = item.gameObject.AddComponent<RemoteInventoryItemId>();
		}

		marker.Id = instanceId;
		marker.OwnerSteamId = ownerSteamId;
	}

	private static void MarkRemoteCloneTree(Item root)
	{
		foreach (var child in root.GetComponentsInChildren<Item>(true))
		{
			if (child == root) // Unity object — ==
			{
				continue;
			}

			if (child.GetComponent<RemoteCloneRender>() == null) // Unity object — ==
			{
				child.gameObject.AddComponent<RemoteCloneRender>();
			}

			var rb = child.GetComponent<Rigidbody2D>();
			if (rb != null) // Unity object — ==
			{
				rb.simulated = false;
			}

			var col = child.GetComponent<Collider2D>();
			if (col != null) // Unity object — ==
			{
				col.enabled = false;
			}
		}
	}
}
