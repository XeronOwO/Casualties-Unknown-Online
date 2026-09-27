using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// Where an open dropdown's option list lives (ticket online-ui-layout-and-input-detail-pass, S3). The
/// game's own <c>TMP_Dropdown</c> builds its list from a template object that sits INSIDE the row prefab, so
/// an instantiated list is a sibling of that template — a child of CUO's page, and therefore inside the
/// page's <c>RectMask2D</c>, inside its layout groups and below every row drawn after it. That is the user's
/// report read from the other side: the options appear behind the window's own rows, are clipped by the page
/// and answer no click.
///
/// <para>
/// This layer is the fix: one small canvas, a child of the window, sorting one step above CUO's own surface,
/// with its own raycaster. Every dropdown CUO builds hands its template over to it, so a list is instantiated
/// outside the page's mask and the layout, and carries the layer's sorting order with it. It is deliberately
/// not a mask and not a layout group: a popup is an overlay, and the only thing it must respect is the top.
/// </para>
/// </summary>
internal sealed class OnlineUiDropdownPopup
{
	/// <summary>The layer's object name in the scene, so a log line can be tied to it.</summary>
	internal const string LayerName = "CUO Online UI Popup Layer";

	private readonly RectTransform _layer;
	private readonly Canvas _canvas;
	private readonly int _sortingOrder;

	private OnlineUiDropdownPopup(RectTransform layer, Canvas canvas, int sortingOrder)
	{
		_layer = layer;
		_canvas = canvas;
		_sortingOrder = sortingOrder;
	}

	/// <summary>Builds the layer as a child of the window, covering it: the list is positioned in the
	/// window's own coordinates, so the layer's rect must be the window's.</summary>
	internal static OnlineUiDropdownPopup Create(RectTransform parent, int surfaceSortingOrder)
	{
		var go = new GameObject(LayerName, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(LayoutElement));
		go.transform.SetParent(parent, worldPositionStays: false);

		// The window's shell is a layout group and this layer is not one of its bands: it covers the frame,
		// while its lists are placed by the game's own dropdown code.
		go.GetComponent<LayoutElement>().ignoreLayout = true;
		var rect = (RectTransform)go.transform;
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;

		var canvas = go.GetComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = surfaceSortingOrder + 1;
		return new OnlineUiDropdownPopup(rect, canvas, canvas.sortingOrder);
	}

	/// <summary>
	/// Takes one dropdown's template out of the page and into the layer. The template is the object the game's
	/// dropdown instantiates its list from, so what it carries here — the layer as its parent, its own canvas
	/// at the layer's sorting order, a raycaster — is what every list it opens inherits.
	/// </summary>
	internal void Adopt(TMP_Dropdown dropdown)
	{
		var template = dropdown.template;
		if (template is null)
		{
			return;
		}

		var listCanvas = template.GetComponent<Canvas>();
		listCanvas ??= template.gameObject.AddComponent<Canvas>();

		listCanvas.overrideSorting = true;
		listCanvas.sortingOrder = _sortingOrder;
		if (template.GetComponent<GraphicRaycaster>() is null)
		{
			template.gameObject.AddComponent<GraphicRaycaster>();
		}

		if (template.parent != _layer)
		{
			template.SetParent(_layer, worldPositionStays: false);
		}

		// A template is not a list: it stays hidden, exactly as the prefab had it.
		if (template.gameObject.activeSelf)
		{
			template.gameObject.SetActive(false);
		}
	}

	/// <summary>
	/// Puts the open list back on top, once per frame the window is applied. A <c>TMP_Dropdown</c> reuses the
	/// list object it built, so a list that lost the sorting order (the surface rebuilt, a canvas was
	/// re-created) would otherwise stay behind the window it belongs to. One list is open at a time, which is
	/// why the last active child is the one normalised, and why this allocates nothing.
	/// </summary>
	internal void Normalise()
	{
		// The layer's own order is re-asserted with the list's: a canvas that lost it (a surface rebuilt under
		// a new root, a scene's canvas recreated) would put every list behind the window it belongs to.
		if (_canvas.sortingOrder != _sortingOrder)
		{
			_canvas.overrideSorting = true;
			_canvas.sortingOrder = _sortingOrder;
		}

		for (var index = _layer.childCount - 1; index >= 0; index--)
		{
			var child = _layer.GetChild(index);
			if (!child.gameObject.activeSelf)
			{
				continue;
			}

			child.SetAsLastSibling();
			if (child.TryGetComponent<Canvas>(out var listCanvas))
			{
				listCanvas.overrideSorting = true;
				listCanvas.sortingOrder = _sortingOrder;
			}

			return;
		}
	}
}
