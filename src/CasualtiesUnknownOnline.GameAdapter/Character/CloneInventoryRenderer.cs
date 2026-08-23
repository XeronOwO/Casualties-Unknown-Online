using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;

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

	internal void ApplyCloneInventory(Body clone, CharacterDataMsg data)
	{
		_log.LogDebug("[CloneRender] apply {Count} items to clone slots ({Slots} slots).", data.Items.Count, clone.slots.Length);
		foreach (var slot in clone.slots)
		{
			if (slot == null) // Unity object — ==
			{
				continue;
			}

			var wanted = data.Items.FirstOrDefault(x => x.SlotIndex == slot.slot);
			RenderItemInto(slot.transform, wanted, slot.spriteSortOrder, wearLimb: null);
		}

		for (var i = 0; i < clone.limbs.Length; i++)
		{
			var limb = clone.limbs[i];
			if (limb == null) // Unity object — ==
			{
				continue;
			}

			var worn = data.Items.FirstOrDefault(x => x.SlotIndex == -(i + 2));
			RenderItemInto(limb.transform, worn, 0, wearLimb: limb);
		}
	}

	/// <summary>
	/// Materialize one snapshot item into a render parent. Slot parents are
	/// fully cleared (a slot only ever holds items); limb parents keep the
	/// game's own children (bones/decorations) and clear only our previous
	/// renders (RemoteCloneRender-marked).
	/// </summary>
	private static void RenderItemInto(Transform parent, CharacterItemMsg? wanted, int sortOrder, Limb? wearLimb)
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
			var matches = parent.GetComponentsInChildren<Item>(true)
				.Where(i => i.id == wanted.ItemId
					&& (wearLimb == null || i.GetComponent<RemoteCloneRender>() != null)).ToArray();
			if (matches.Length > 0)
			{
				// Keep the first; destroy any further copies — the reason the
				// old diff (GetChild(0)-only) was abandoned was stray duplicates
				// accumulating in a slot; the incremental path must not resurrect
				// them.
				for (var i = 1; i < matches.Length; i++)
				{
					UnityEngine.Object.Destroy(matches[i].gameObject);
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
				UnityEngine.Object.Destroy(parent.GetChild(c).gameObject);
			}
		}
		else
		{
			for (var c = parent.childCount - 1; c >= 0; c--)
			{
				var child = parent.GetChild(c);
				if (child.GetComponent<RemoteCloneRender>() != null) // Unity object — ==
				{
					UnityEngine.Object.Destroy(child.gameObject);
				}
			}
		}

		if (wanted is null)
		{
			return;
		}

		var prefab = Resources.Load(wanted.ItemId);
		if (prefab == null) // Unity object — ==
		{
			return;
		}

		var obj = UnityEngine.Object.Instantiate(prefab, parent) as GameObject;
		obj!.transform.localPosition = Vector3.zero;
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

		// Every clone render gets the marker now: limb parents still use it to
		// clear only our renders, and the uniform marker lets presentation code
		// identify a display-proxy item without depending on slot internals.
		obj.AddComponent<RemoteCloneRender>();
	}
}
