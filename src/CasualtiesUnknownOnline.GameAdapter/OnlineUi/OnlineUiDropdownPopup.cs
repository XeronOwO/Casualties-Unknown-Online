using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Object = UnityEngine.Object;

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
/// not a mask and not a layout group: a popup is an overlay, and the only thing it must respect is the field
/// it belongs to.
/// </para>
///
/// <para>
/// Moving the template out of the row is not enough on its own. A template's rect is expressed in ITS OWN
/// row's coordinates — the game's prefab puts it 46 units below the dropdown's top with the height its list
/// may take — and once its parent is the layer those numbers resolve against the whole window. Batch
/// `20261018-t` read the result: the open list was 980×−20 at the window's top edge, not under the 150-wide
/// field, because CUO's box pass had also written the row's own insets onto the template. The layer therefore
/// OWNS the template's rect from the moment it adopts it: it keeps the height the prefab authored (the list's
/// maximum), and every frame the window is applied it puts the template at the field's bottom edge and the
/// open list on the side of the field the canvas has room for. The field's own rect is read from the live
/// <c>RectTransform</c>, so a dragged window, a scrolled page or a rebuilt row moves the list with it.
/// </para>
///
/// <para>
/// One template per dropdown, and one open list at a time. The game's own outside-click blocker is created
/// one step BELOW CUO's surface, so a click on a second field is not swallowed by it and would open a second
/// list; the layer closes the first instead (a list whose field is no longer the open one would otherwise be
/// placed on the wrong field), and a template whose dropdown is gone with its rebuilt row is destroyed here
/// because the layer, not the row, is its parent.
/// </para>
/// </summary>
internal sealed class OnlineUiDropdownPopup
{
	/// <summary>The layer's object name in the scene, so a log line can be tied to it.</summary>
	internal const string LayerName = "CUO Online UI Popup Layer";

	private readonly RectTransform _layer;
	private readonly Canvas _canvas;
	private readonly int _sortingOrder;
	private readonly ILogger _log;
	private readonly List<Adopted> _adopted = [];
	private readonly Vector3[] _corners = new Vector3[4];

	private OnlineUiDropdownPopup(RectTransform layer, Canvas canvas, int sortingOrder, ILogger log)
	{
		_layer = layer;
		_canvas = canvas;
		_sortingOrder = sortingOrder;
		_log = log;
	}

	/// <summary>One dropdown whose template this layer has taken over: the template itself, plus the height
	/// the game's prefab authored for the list it builds (the maximum a list may take, read once because CUO's
	/// box pass is what overwrote it before this layer existed).</summary>
	private sealed class Adopted(TMP_Dropdown dropdown, RectTransform template, float maxHeight)
	{
		internal TMP_Dropdown Dropdown { get; } = dropdown;

		internal RectTransform Template { get; } = template;

		internal float MaxHeight { get; } = maxHeight;
	}

	/// <summary>Builds the layer as a child of the window, covering it: the list is positioned in the
	/// window's own coordinates, so the layer's rect must be the window's.</summary>
	internal static OnlineUiDropdownPopup Create(RectTransform parent, int surfaceSortingOrder, ILogger log)
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
		return new OnlineUiDropdownPopup(rect, canvas, canvas.sortingOrder, log);
	}

	/// <summary>
	/// Takes one dropdown's template out of the page and into the layer, once per dropdown. The template is the
	/// object the game's dropdown instantiates its list from, so what it carries here — the layer as its
	/// parent, its own canvas at the layer's sorting order, a raycaster — is what every list it opens
	/// inherits. Its authored height is captured on the way in, because it is the list's own maximum and the
	/// position CUO writes it to every frame is not where the prefab put it.
	/// </summary>
	internal void Adopt(TMP_Dropdown dropdown)
	{
		var template = dropdown.template;
		if (template is null)
		{
			return;
		}

		if (Find(dropdown) is not null)
		{
			return;
		}

		// The list's own maximum, read before anything CUO owns writes the template's rect: the game's
		// dropdown never resizes the list past it, and a zero or negative answer would leave the popup with
		// no height at all.
		var maxHeight = template.rect.height > 0f ? template.rect.height : _layer.rect.height;

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

		var adopted = new Adopted(dropdown, template, maxHeight);
		_adopted.Add(adopted);
		Place(adopted);
	}

	/// <summary>
	/// Puts every adopted template, and the open list among them, back on top and onto its field, once per
	/// frame the window is applied. A <c>TMP_Dropdown</c> reuses the list object it built, so a list that lost
	/// the sorting order (the surface rebuilt, a canvas was re-created) would otherwise stay behind the window
	/// it belongs to; a field that moved (a scrolled page) would otherwise leave its list behind. One list is
	/// open at a time — a second field closes the first here — so the last active child is the list to
	/// normalise, and this allocates nothing. A template whose dropdown was destroyed with its row is
	/// destroyed here too: the layer is the template's parent now, so the row's destruction would otherwise
	/// leave it behind for ever.
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

		var open = NormaliseAdopted();
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

			if (open is not null && child is RectTransform list)
			{
				PlaceList(list, open);
			}

			return;
		}
	}

	/// <summary>Prunes the templates whose dropdown is gone, keeps every live template on its field, closes
	/// every open list but the newest (a second field opened while one list is up), and answers the dropdown
	/// whose list is open — the one field the open list has to sit on.</summary>
	private TMP_Dropdown? NormaliseAdopted()
	{
		for (var index = _adopted.Count - 1; index >= 0; index--)
		{
			var adopted = _adopted[index];
			if (adopted.Template == null || adopted.Dropdown == null) // Unity objects — ==
			{
				if (adopted.Template != null) // Unity object — !=
				{
					_log.LogDebug("Online UI window: the popup layer dropped the template of a dropdown whose row was rebuilt.");
					Object.Destroy(adopted.Template.gameObject);
				}

				_adopted.RemoveAt(index);
			}
		}

		TMP_Dropdown? open = null;
		foreach (var adopted in _adopted)
		{
			Place(adopted);
			if (adopted.Dropdown.IsExpanded)
			{
				open = adopted.Dropdown;
			}
		}

		if (open is not null)
		{
			foreach (var adopted in _adopted)
			{
				if (adopted.Dropdown != open && adopted.Dropdown.IsExpanded) // Unity objects — !=
				{
					adopted.Dropdown.Hide();
				}
			}
		}

		return open;
	}

	private Adopted? Find(TMP_Dropdown dropdown)
	{
		foreach (var adopted in _adopted)
		{
			if (adopted.Dropdown == dropdown) // Unity objects — ==
			{
				return adopted;
			}
		}

		return null;
	}

	/// <summary>Puts one adopted template at its field's bottom edge, as wide as the field, at the height the
	/// prefab authored for a list. The template is only a factory and a starting rect: the side the LIST opens
	/// on is decided when it is placed (<see cref="PlaceList"/>), because only the open list's own height can
	/// answer it.</summary>
	private void Place(Adopted adopted)
	{
		if (TryFieldBox(adopted.Dropdown, out var center, out var bottom, out _, out var width))
		{
			PlaceAt(adopted.Template, center, bottom, width, adopted.MaxHeight, opensAbove: false);
		}
	}

	/// <summary>
	/// Puts the open list at its field, keeping the height the game's own dropdown code gave it and choosing the
	/// side the canvas has room for: below when it fits, above when it does not fit below but does fit above
	/// (<see cref="OnlineUiDropdownPlacement.SideFor"/>). The game's own flip only knows the parent it is given
	/// — the whole layer — so its mirrored position would be wrong; the layer answers the same question with the
	/// field and the canvas.
	/// </summary>
	private void PlaceList(RectTransform list, TMP_Dropdown owner)
	{
		var height = list.rect.height;
		if (height <= 0f || !TryFieldBox(owner, out var center, out var bottom, out var top, out var width))
		{
			return;
		}

		var opensAbove = false;
		if (owner.GetComponentInParent<Canvas>()?.rootCanvas is { } root
			&& root.transform is RectTransform canvas
			&& canvas.rect.height > 0f)
		{
			var fieldBottom = canvas.InverseTransformPoint(_corners[0]).y;
			var fieldTop = canvas.InverseTransformPoint(_corners[1]).y;
			opensAbove = OnlineUiDropdownPlacement.SideFor(
				height,
				fieldBottom - canvas.rect.yMin,
				canvas.rect.yMax - fieldTop) == OnlineUiDropdownPlacement.OpenSide.Above;
		}

		PlaceAt(list, center, opensAbove ? top : bottom, width, height, opensAbove);
	}

	/// <summary>
	/// The field's own box in the layer's coordinates: the horizontal centre of the dropdown's rect, the y of
	/// its bottom and top edges, and its width. The rectangle is read from the live transform — a dragged
	/// window or a scrolled page changes it — and the corners are converted into the layer's space, which is
	/// the space a child's anchors are written in. The world corners stay in <see cref="_corners"/> for the
	/// caller that also asks the canvas for the room around this field.
	/// </summary>
	private bool TryFieldBox(TMP_Dropdown? dropdown, out float center, out float bottom, out float top, out float width)
	{
		center = 0f;
		bottom = 0f;
		top = 0f;
		width = 0f;
		if (dropdown is null || dropdown.transform is not RectTransform field)
		{
			return false;
		}

		field.GetWorldCorners(_corners);
		var bottomLeft = _layer.InverseTransformPoint(_corners[0]);
		var bottomRight = _layer.InverseTransformPoint(_corners[3]);
		var topLeft = _layer.InverseTransformPoint(_corners[1]);
		width = bottomRight.x - bottomLeft.x;
		if (width <= 0f)
		{
			return false;
		}

		center = bottomLeft.x + (width * 0.5f);
		bottom = bottomLeft.y;
		top = topLeft.y;
		return true;
	}

	/// <summary>Writes one rect's anchor, pivot and size so that its top edge (or its bottom edge, when it
	/// opens above) sits on the given point of the layer — the field's bottom or top edge — and it hangs on
	/// that side. All four numbers are written every time, so a list the game's own flip has re-anchored is put
	/// back on its field instead of being mirrored across the whole window.</summary>
	private void PlaceAt(RectTransform rect, float center, float edge, float width, float height, bool opensAbove)
	{
		var layer = _layer.rect;
		if (layer.width <= 0f || layer.height <= 0f)
		{
			return;
		}

		var anchor = new Vector2(
			OnlineUiDropdownPlacement.AnchorX(center, layer.xMin, layer.width),
			OnlineUiDropdownPlacement.AnchorY(edge, layer.yMin, layer.height));
		rect.anchorMin = anchor;
		rect.anchorMax = anchor;
		rect.pivot = new Vector2(0.5f, opensAbove ? 0f : 1f);
		rect.anchoredPosition = Vector2.zero;
		rect.sizeDelta = new Vector2(width, height);
	}
}
