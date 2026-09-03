using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Per-instance visual state for a custom item template. The Game Adapter
/// attaches this component when a <see cref="ModItemVisual"/> is authored; it
/// remembers the base sprite/sorting order and holds the resolved worn-sprite,
/// multi-limb worn-sprite and liquid-mask resources so the wear/drop and
/// water-container paths can restore the item's normal presentation without
/// consulting the mod definition again.
/// </summary>
internal sealed class CustomItemVisualState : MonoBehaviour
{
	internal Sprite? NormalSprite { get; set; }
	internal Sprite? WornSprite { get; set; }
	internal Vector2 WornOffset { get; set; }
	internal bool HasWornSprite { get; set; }
	internal bool HasWornSortingOrder { get; set; }
	internal int WornSortingOrder { get; set; }
	internal int NormalSortingOrder { get; set; }
	internal Sprite? LiquidMaskSprite { get; set; }
	internal bool HasLiquidMask { get; set; }

	private readonly List<MultiWornVisualEntry> _multiWornSprites = [];

	internal bool HasMultiWornSprites => _multiWornSprites.Count > 0;

	internal void AddMultiWornSprite(string limbName, Sprite sprite, Vector2 offset)
	{
		if (string.IsNullOrWhiteSpace(limbName) || sprite == null) // Unity object — ==
		{
			return;
		}

		_multiWornSprites.Add(new MultiWornVisualEntry(limbName, sprite, offset));
	}

	internal void ApplyWornVisual()
	{
		var renderer = GetComponent<SpriteRenderer>();
		if (renderer != null) // Unity object — ==
		{
			if (HasWornSprite && WornSprite != null) // Unity object — ==
			{
				renderer.sprite = WornSprite;
			}

			if (HasWornSortingOrder)
			{
				renderer.sortingOrder = WornSortingOrder;
			}
		}

		if (HasWornSprite)
		{
			var local = transform.localPosition;
			transform.localPosition = new Vector3(WornOffset.x, WornOffset.y, local.z);
		}
	}

	internal void RestoreNormalVisual()
	{
		var renderer = GetComponent<SpriteRenderer>();
		if (renderer != null) // Unity object — ==
		{
			if (NormalSprite != null) // Unity object — ==
			{
				renderer.sprite = NormalSprite;
			}

			if (HasWornSortingOrder)
			{
				renderer.sortingOrder = NormalSortingOrder;
			}
		}

		var local = transform.localPosition;
		transform.localPosition = new Vector3(0f, 0f, local.z);
	}

	internal void ApplyLiquidMask()
	{
		if (!HasLiquidMask || LiquidMaskSprite == null) // Unity object — ==
		{
			return;
		}

		var water = GetComponent<WaterContainerItem>();
		if (water != null) // Unity object — ==
		{
			water.fillSprite = LiquidMaskSprite;
		}

		var fillRenderer = FindLiquidFillRenderer();
		if (fillRenderer != null) // Unity object — ==
		{
			fillRenderer.sprite = LiquidMaskSprite;
		}
	}

	/// <summary>
	/// Configure the vanilla <see cref="Wearable"/> secondary-sprite arrays from
	/// this item's authored multi-limb entries. When <paramref name="body"/> is
	/// provided, entries whose limb does not exist on that body are filtered out
	/// so the vanilla <c>CreateSprites</c> path never dereferences a missing
	/// limb. An empty configuration is still normalized for custom items that
	/// added a <see cref="Wearable"/> component but authored no secondary sprites.
	/// </summary>
	internal void ConfigureWearableSecondarySprites(Wearable wearable, Body? body)
	{
		if (wearable == null) // Unity object — ==
		{
			return;
		}

		if (!HasMultiWornSprites)
		{
			if (wearable.secondaryLimbs == null)
			{
				wearable.secondaryLimbs = [];
				wearable.secondaryLimbSprites = [];
				wearable.secondaryObjects = [];
			}

			return;
		}

		var entries = _multiWornSprites
			.Where(entry => entry.Sprite != null // Unity object — ==
				&& (body == null || body.LimbByName(entry.LimbName) != null)) // Unity objects — ==
			.ToArray();

		wearable.secondaryLimbs = [.. entries.Select(entry => entry.LimbName)];
		wearable.secondaryLimbSprites = [.. entries.Select(entry => entry.Sprite)];
		wearable.secondaryObjects = new GameObject[entries.Length];
	}

	/// <summary>
	/// Apply authored offsets and the optional sorting-order override to the
	/// secondary sprite objects just created by <see cref="Wearable.CreateSprites"/>.
	/// </summary>
	internal void ApplySecondarySpritePresentation(Wearable wearable)
	{
		if (wearable == null || wearable.secondaryLimbs == null || wearable.secondaryObjects == null) // Unity object — ==
		{
			return;
		}

		var count = Math.Min(wearable.secondaryLimbs.Length, wearable.secondaryObjects.Length);
		for (var i = 0; i < count; i++)
		{
			var obj = wearable.secondaryObjects[i];
			var limb = wearable.secondaryLimbs[i];
			if (obj == null || string.IsNullOrWhiteSpace(limb)) // Unity object — ==
			{
				continue;
			}

			var entry = FindMultiWornSprite(limb);
			if (entry is null)
			{
				continue;
			}

			var local = obj.transform.localPosition;
			obj.transform.localPosition = new Vector3(entry.Offset.x, entry.Offset.y, local.z);
			if (HasWornSortingOrder)
			{
				var renderer = obj.GetComponent<SpriteRenderer>();
				if (renderer != null) // Unity object — ==
				{
					renderer.sortingOrder = WornSortingOrder;
				}
			}
		}
	}

	/// <summary>
	/// Materialize the additive multi-limb sprites for paths that never run the
	/// vanilla <c>WearWearable</c> flow (remote clone rendering and reconnect
	/// restore). The method configures the wearable arrays, calls the vanilla
	/// <c>CreateSprites</c> path, then applies the authored offsets/sorting order.
	/// </summary>
	internal void EnsureSecondarySprites(Body? body)
	{
		if (body == null || !HasMultiWornSprites) // Unity object — ==
		{
			return;
		}

		var wearable = GetComponent<Wearable>();
		if (wearable == null) // Unity object — ==
		{
			return;
		}

		ConfigureWearableSecondarySprites(wearable, body);
		wearable.CreateSprites(body);
		ApplySecondarySpritePresentation(wearable);
	}

	private MultiWornVisualEntry? FindMultiWornSprite(string limbName)
	{
		foreach (var entry in _multiWornSprites)
		{
			if (string.Equals(entry.LimbName, limbName, StringComparison.OrdinalIgnoreCase))
			{
				return entry;
			}
		}

		return null;
	}

	private SpriteRenderer? FindLiquidFillRenderer()
	{
		var renderers = GetComponentsInChildren<SpriteRenderer>(true);
		foreach (var renderer in renderers)
		{
			if (renderer != null
				&& renderer.transform.parent == transform
				&& string.Equals(renderer.gameObject.name, "LiquidFill", System.StringComparison.Ordinal))
			{
				return renderer;
			}
		}

		return null;
	}

	private sealed class MultiWornVisualEntry
	{
		internal MultiWornVisualEntry(string limbName, Sprite sprite, Vector2 offset)
		{
			LimbName = limbName;
			Sprite = sprite;
			Offset = offset;
		}

		internal string LimbName { get; }
		internal Sprite Sprite { get; }
		internal Vector2 Offset { get; }
	}
}
