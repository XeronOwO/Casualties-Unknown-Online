using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// One CUO panel on the game's own canvas (ticket online-ui-art-and-controls-overhaul, S5): the quick
/// panel and the in-world player context menu, which are the same shape — a titled panel of the game's own
/// row controls — and therefore the same view, built twice.
///
/// <para>
/// It is the window's shell without the window: the frame reads the game's own row prefab for its sprite,
/// <c>Image.type</c> and pixels-per-unit multiplier, every label takes that prefab's font and size, and the
/// rows go through the same <see cref="OnlineUiWindowRowView"/> controls the pages use (a control is reused
/// when its kind and id still match, written only where it changed, destroyed when the model drops it).
/// The panel's height is its content's; its width is the model's.
/// </para>
///
/// <para>
/// Where it hangs is the model's: docked in the canvas's bottom-right corner (the quick panel's IMGUI rect,
/// kept) or at the screen point the player clicked, placed through the Runtime's own clamp
/// (<see cref="OnlineUiPanelPlacement"/>) once the panel has been laid out — the same split the window
/// makes with its wrapping rule.
/// </para>
///
/// <para>
/// The pointer over the panel's rect is POLLED and reported as a flip, for the reason the launcher and the
/// window poll: uGUI's enter/exit callbacks fire on movement, and both world input paths judge the click by
/// where the pointer is. Which two intents a flip reports is the caller's, because the two panels are two
/// facts in the census.
/// </para>
/// </summary>
internal sealed class OnlineUiPanelView
{
	/// <summary>The gap between the panel's frame and its contents.</summary>
	internal const float ContentPadding = 6f;

	internal const float LineSpacing = 4f;

	internal const float TitleHeight = 24f;

	internal const float CloseWidth = 22f;

	internal const float CloseHeight = 20f;

	/// <summary>The gap the docked panel keeps from the canvas's own bottom-right corner.</summary>
	internal const float DockMargin = 16f;

	/// <summary>The height of an empty model row — the panel's breathing room.</summary>
	private const float GapHeight = 8f;

	internal const string CloseCaption = "×";

	/// <summary>The panel's frame tint, laid over the game's own sprite so the shape is the game's and the
	/// surface is CUO's — the pair the window's frame uses.</summary>
	private static readonly Color PanelTint = new(0.035f, 0.045f, 0.06f, 0.97f);

	private static readonly Color TitleTint = new(0.07f, 0.09f, 0.12f, 0.98f);

	private readonly Action<OnlineUiIntent> _report;
	private readonly OnlineUiIntentKind _hoverEntered;
	private readonly OnlineUiIntentKind _hoverLeft;
	private readonly GameObject _root;
	private readonly RectTransform _rect;
	private readonly Canvas? _canvas;
	private readonly RectTransform _titleBar;
	private readonly TextMeshProUGUI _title;
	private readonly OnlineUiViewSource _views;
	private readonly OnlineUiControlTypography _typography;
	private readonly List<OnlineUiWindowRowView> _rows = [];

	/// <summary>The width each element of a row was laid out at, kept per row so the panel's column pass can
	/// lay every element out again without measuring anything twice.</summary>
	private readonly List<float[]> _widths = [];

	private OnlineUiControlView? _close;
	private string _closeId = "\0";
	private string _titleText = "\0";
	private bool _hovered;
	private bool _structureDirty;
	private float _width = float.NaN;
	private OnlineUiPanelAnchor? _anchor;
	private Vector2 _point;

	private OnlineUiPanelView(
		ILogger log,
		Action<OnlineUiIntent> report,
		OnlineUiIntentKind hoverEntered,
		OnlineUiIntentKind hoverLeft,
		GameObject root,
		RectTransform rect,
		Canvas? canvas,
		RectTransform titleBar,
		TextMeshProUGUI title,
		OnlineUiControlTypography typography)
	{
		_views = new OnlineUiViewSource(log, "Online UI panel", typography, report);
		_report = report;
		_hoverEntered = hoverEntered;
		_hoverLeft = hoverLeft;
		_root = root;
		_rect = rect;
		_canvas = canvas;
		_titleBar = titleBar;
		_title = title;
		_typography = typography;
	}

	/// <summary>Builds a panel under CUO's canvas. It starts hidden: a frame that carries a panel model is
	/// what shows it. <paramref name="hoverEntered"/> and <paramref name="hoverLeft"/> are the two intents
	/// this panel's pointer flips report.</summary>
	internal static OnlineUiPanelView Create(
		string name,
		Transform parent,
		ILogger log,
		Action<OnlineUiIntent> report,
		OnlineUiIntentKind hoverEntered,
		OnlineUiIntentKind hoverLeft)
	{
		var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
		root.transform.SetParent(parent, worldPositionStays: false);
		var rect = (RectTransform)root.transform;
		rect.localScale = Vector3.one;

		OnlineUiControlFactory.ReadRowTemplate(root.transform, out var typography, out var sprite, out var imageType, out var pixelsPerUnit);

		// The panel keeps the game's own border exactly as the window does (S4): the sprite stays UNTINTED on
		// the panel's own image — the sprite is what carries the game's border — and the dark surface the
		// panel reads on is a fill inside it that ignores the panel's layout group, because it is chrome and
		// not a row.
		OnlineUiControlFactory.MakeFrame(root, sprite, imageType, pixelsPerUnit, PanelTint);
		var panel = root.GetComponent<Image>();

		// The panel is a raycast target on purpose: a click inside it belongs to CUO's surface and must not
		// fall through to the game's menu or the world behind it.
		panel.raycastTarget = true;

		var layout = root.GetComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset((int)ContentPadding, (int)ContentPadding, (int)ContentPadding, (int)ContentPadding);
		layout.spacing = LineSpacing;
		layout.childAlignment = TextAnchor.UpperLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		// The width is the model's and the height is the content's, which is what the fitter is for.
		var fitter = root.GetComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		var titleBar = OnlineUiSurfaceParts.CreateRect("Title Bar", root.transform, typeof(Image), typeof(LayoutElement));
		var titleImage = titleBar.GetComponent<Image>();
		titleImage.sprite = sprite;
		titleImage.type = imageType;
		titleImage.pixelsPerUnitMultiplier = pixelsPerUnit;
		titleImage.color = TitleTint;
		titleImage.raycastTarget = true;
		var titleLayout = titleBar.GetComponent<LayoutElement>();
		titleLayout.minHeight = TitleHeight;
		titleLayout.preferredHeight = TitleHeight;

		var title = OnlineUiSurfaceParts.CreateLabel("Title", titleBar, typography, OnlineUiTextStyle.Title);
		OnlineUiSurfaceParts.Stretch(title.rectTransform, 4f, 4f, 0f, 0f);
		title.alignment = TextAlignmentOptions.MidlineLeft;

		var view = new OnlineUiPanelView(
			log,
			report,
			hoverEntered,
			hoverLeft,
			root,
			rect,
			root.GetComponentInParent<Canvas>()?.rootCanvas,
			titleBar,
			title,
			typography);
		view.SetVisible(false);
		return view;
	}

	/// <summary>Shows or hides the whole panel; a hidden panel is still there, so reopening it does not
	/// rebuild its controls.</summary>
	internal void SetVisible(bool visible)
	{
		if (_root.activeSelf != visible)
		{
			_root.SetActive(visible);
		}
	}

	/// <summary>Applies one frame's model: the title, the close control, the rows and the panel's own
	/// placement, each written only where it differs from what is already shown.</summary>
	internal void Apply(OnlineUiPanelModel model)
	{
		if (_titleText != model.Title)
		{
			_titleText = model.Title;
			_title.text = model.Title;
		}

		ApplyClose(model.CloseId);
		ApplyWidth(model.Width);
		ApplyRows(model.Rows);
		if (_structureDirty)
		{
			// Only the ORDER is repaired here, for the same reason the window's page repairs nothing else:
			// every size a row's group reads is declared to it as a LayoutElement value, which uGUI marks its
			// own layout dirty for. The one rebuild this panel needs is in PlaceAtPoint, where the panel's own
			// height is read.
			_structureDirty = false;
			Reorder();
		}

		ApplyAnchor(model);

		if (_anchor == OnlineUiPanelAnchor.Point)
		{
			_point = new Vector2(model.X, model.Y);
			PlaceAtPoint();
		}
	}

	/// <summary>
	/// Polls the pointer against the panel's rect and queues the hover fact when it flips. It runs while
	/// the panel is hidden too, so closing it under the pointer still reports the pointer as out.
	/// </summary>
	internal void PollPointer(Queue<OnlineUiIntent> intents)
	{
		var camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
		var hovered = _root.activeInHierarchy
			&& RectTransformUtility.RectangleContainsScreenPoint(_rect, Input.mousePosition, camera);
		if (hovered == _hovered)
		{
			return;
		}

		_hovered = hovered;
		intents.Enqueue(new OnlineUiIntent(hovered ? _hoverEntered : _hoverLeft));
	}

	internal void Destroy() => Object.Destroy(_root);

	/// <summary>
	/// The close control is chrome, so the surface builds it; its id comes from the model and its meaning
	/// is the plugin's. A model that carries no id (the context menu) has none — an element with an empty
	/// id reports nothing, which is exactly what a close control nobody asked for should do.
	/// </summary>
	private void ApplyClose(string closeId)
	{
		if (_closeId == closeId)
		{
			return;
		}

		_closeId = closeId;
		if (_close != null)
		{
			_close.Destroy();
			_close = null;
		}

		if (closeId.Length == 0)
		{
			OnlineUiSurfaceParts.Stretch(_title.rectTransform, 4f, 4f, 0f, 0f);
			return;
		}

		_close = OnlineUiControlView.Create(
			OnlineUiElementModel.Button(closeId, CloseCaption, CloseWidth),
			_titleBar,
			_typography,
			_report);
		var closeRect = (RectTransform)_close.Root.transform;
		closeRect.anchorMin = new Vector2(1f, 0.5f);
		closeRect.anchorMax = new Vector2(1f, 0.5f);
		closeRect.pivot = new Vector2(1f, 0.5f);
		closeRect.anchoredPosition = new Vector2(-2f, 0f);
		closeRect.sizeDelta = new Vector2(CloseWidth, CloseHeight);
		OnlineUiSurfaceParts.Stretch(_title.rectTransform, 4f, CloseWidth + 6f, 0f, 0f);
	}

	private void ApplyRows(IReadOnlyList<OnlineUiRowModel> rows)
	{
		while (_rows.Count > rows.Count)
		{
			_rows[_rows.Count - 1].Destroy();
			_rows.RemoveAt(_rows.Count - 1);
			_widths.RemoveAt(_widths.Count - 1);
			_structureDirty = true;
		}

		for (var index = 0; index < rows.Count; index++)
		{
			if (index >= _rows.Count)
			{
				_rows.Add(new OnlineUiWindowRowView(LineSpacing));
				_widths.Add([]);
				_structureDirty = true;
			}

			ApplyRow(index, _rows[index], rows[index]);
		}

		var column = OnlineUiWindowRowView.ColumnWidthOf(rows, NameWidth, ContentWidth);
		for (var index = 0; index < rows.Count; index++)
		{
			_rows[index].AlignToColumn(rows[index].Elements, _widths[index], column);
		}
	}

	/// <summary>
	/// The width one element's NAME needs on this surface: the view this panel already holds for it, measured
	/// on its label (<see cref="OnlineUiControlView.NameWidthOf"/>) — the same reading the window's pages take,
	/// so a control lines up the same way wherever it is drawn.
	/// </summary>
	private float NameWidth(int rowIndex, int index, OnlineUiElementModel element) =>
		rowIndex < _rows.Count && index < _rows[rowIndex].Elements.Count
			? _rows[rowIndex].Elements[index].NameWidthOf(element)
			: 0f;

	/// <summary>
	/// One model row on the panel, at its own index in the panel's list: the views, the wrap the model's own
	/// breaks and the fit together produce, and every element placed on the line it landed on. The widths it
	/// computed are kept for the column pass above, which runs once every row has its own.
	/// </summary>
	private void ApplyRow(int rowIndex, OnlineUiWindowRowView row, OnlineUiRowModel model)
	{
		var elements = model.Elements;
		if (elements.Count == 0)
		{
			// An empty row is the panel's room: one line with nothing in it but the room the model asked for —
			// the same rows the window's pages use, so a panel's blocks breathe like a page's. A model that
			// names no room keeps the panel's own gap.
			_structureDirty |= row.EnsureLines(_root.transform, 1);
			row.SetGap(0, model.Gap > 0f ? model.Gap : GapHeight);
			_structureDirty |= row.DestroyElementsFrom(0);
			_widths[rowIndex] = [];
			return;
		}

		// Views first and the wrap after them: how wide a control is comes from its own content, so the
		// model's width is only a floor and the lines are decided from what the controls actually take — the
		// same rule the window applies, because this view IS the window's shell without the window. The model
		// is applied before the widths are read because a name is one of the things read here.
		_structureDirty |= _views.Sync(row.Elements, elements, _root.transform);

		var widths = new float[elements.Count];
		var breaks = new bool[elements.Count];
		for (var index = 0; index < elements.Count; index++)
		{
			row.Elements[index].Apply(elements[index], aloneOnItsLine: false);
			widths[index] = row.Elements[index].EffectiveWidth(elements[index]);
			breaks[index] = elements[index].NewLine;
		}

		var lines = OnlineUiRowLayout.LineOf(widths, breaks, ContentWidth, LineSpacing);
		var owners = OnlineUiRowLayout.LineOwners(breaks, lines);
		row.Owners = owners;
		_widths[rowIndex] = widths;
		var lineCount = lines[lines.Length - 1] + 1;
		_structureDirty |= row.EnsureLines(_root.transform, lineCount);
		for (var index = 0; index < lineCount; index++)
		{
			row.SetGap(index, -1f);
		}

		for (var index = 0; index < elements.Count; index++)
		{
			var view = row.Elements[index];

			// The same answer the window asks for: the line the wrap gave this element, or the paired row that
			// owns it — the elements beside a paired control are laid out inside it.
			view.SetParent(row.ParentOf(index, lines[index], owners));

			// "Alone on its line" is the LINE's own fact: a paired control puts one element on its first line
			// and two on its second, and only the element with its line to itself takes what that line has
			// left.
			view.Apply(elements[index], OnlineUiWindowView.AloneOnItsLine(lines, index));
			view.LayOutRow(elements[index], widths[index], columnWidth: 0f);
		}

		_structureDirty |= row.DestroyElementsFrom(elements.Count);
	}

	/// <summary>Puts the lines back in model order, with the title bar before them. Unity appends a new object
	/// to the end of its parent, so a row that gained a line would otherwise draw below every row after it.
	/// The frame's own fill is chrome and stays behind both, so the header keeps its own tint.</summary>
	private void Reorder()
	{
		foreach (var row in _rows)
		{
			foreach (var line in row.Lines)
			{
				line.transform.SetAsLastSibling();
			}
		}

		_titleBar.SetSiblingIndex(Mathf.Min(1, _root.transform.childCount - 1));
	}

	private void ApplyWidth(float width)
	{
		if (_width.Equals(width))
		{
			return;
		}

		_width = width;
		_rect.sizeDelta = new Vector2(width, 0f);
	}

	/// <summary>
	/// The docked panel hangs in the canvas's bottom-right corner; the point-anchored one is placed by
	/// <see cref="PlaceAtPoint"/> once its content is laid out.
	/// </summary>
	private void ApplyAnchor(OnlineUiPanelModel model)
	{
		if (_anchor == model.Anchor)
		{
			return;
		}

		_anchor = model.Anchor;
		if (model.Anchor == OnlineUiPanelAnchor.BottomRight)
		{
			_rect.anchorMin = new Vector2(1f, 0f);
			_rect.anchorMax = new Vector2(1f, 0f);
			_rect.pivot = new Vector2(1f, 0f);
			_rect.anchoredPosition = new Vector2(-DockMargin, DockMargin);
			return;
		}

		_rect.anchorMin = new Vector2(0f, 0f);
		_rect.anchorMax = new Vector2(0f, 0f);
		_rect.pivot = new Vector2(0f, 1f);
	}

	/// <summary>
	/// Places the panel's TOP-LEFT corner at the model's screen point, clamped by the Runtime's rule. The
	/// layout is rebuilt first because the rule needs the panel's own height, and a
	/// <see cref="ContentSizeFitter"/> only writes it at the end of the frame, and the pointer is converted
	/// into the parent's space first so that the pointer, the panel's size and the screen are all in the
	/// canvas's OWN units — the surface is scaled by the game's UI scale, and a clamp that mixed pixels with
	/// canvas units would only hold at scale 1 (the independent review's F1).
	/// </summary>
	private void PlaceAtPoint()
	{
		if (_rect.parent is not RectTransform parent)
		{
			return;
		}

		LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
		var camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, _point, camera, out var pointer))
		{
			return;
		}

		// The parent rect IS the screen for this surface (CUO's canvas covers the game's), so its own bounds
		// are the screen in canvas units, and the corner comes back relative to those bounds' bottom-left.
		var bounds = parent.rect;
		var corner = OnlineUiPanelPlacement.ForPointer(
			pointer.x - bounds.xMin,
			pointer.y - bounds.yMin,
			_rect.rect.width,
			_rect.rect.height,
			bounds.width,
			bounds.height);

		// The pivot IS the top-left corner, and a rect's local position is its pivot's — the anchors above
		// only decide what the rect is positioned against when the parent changes size.
		_rect.localPosition = new Vector3(bounds.xMin + corner.X, bounds.yMin + corner.Top, 0f);
	}

	/// <summary>The width the rows are wrapped against: the panel minus its own padding.</summary>
	private float ContentWidth => Mathf.Max(1f, _width - (2f * ContentPadding));
}
