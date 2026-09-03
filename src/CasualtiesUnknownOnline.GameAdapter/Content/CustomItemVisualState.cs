using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Per-instance visual state for a custom item template. The Game Adapter
/// attaches this component when a <see cref="ModItemVisual"/> is authored; it
/// remembers the base sprite/sorting order and holds the resolved worn-sprite
/// and liquid-mask resources so the wear/drop and water-container paths can
/// restore the item's normal presentation without consulting the mod definition
/// again.
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
}
