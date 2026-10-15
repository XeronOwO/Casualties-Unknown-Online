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
/// The CUO Online UI window's shell on the game's own canvas (ticket online-ui-art-and-controls-overhaul,
/// S2b; its geometry re-cut by online-ui-layout-and-input-detail-pass, S1–S3): the frame, the title bar the
/// player drags, the close control, the tab row and the scrolling page of the game's own rows.
///
/// <para>
/// It is driven by <see cref="OnlineUiWindowModel"/> — one model per frame while the window is open — and
/// it keeps every control it built alive across frames, re-applying only what changed (the reconcile path
/// the launcher's caption and alpha already use). A control is reused in place when its kind and id still
/// match, so a roster that gained one member does not rebuild every card below it; a control the model no
/// longer carries is destroyed.
/// </para>
///
/// <para>
/// The frame's own art is the game's: the sprite, its <c>Image.type</c> and its pixels-per-unit multiplier
/// are read from the game's own row prefab (the same one the launcher is built from), the frame keeps them
/// UNTINTED — that is what shows the game's own border — and the dark surface the window reads on is a fill
/// laid inside it. The typography of every label this surface creates comes from that prefab's label. The
/// tint of the fill is CUO's, and it is the only colour the surface owns: every content colour travels in
/// the model.
/// </para>
///
/// <para>
/// Where the shell's bands sit is <see cref="OnlineUiWindowLayout"/>'s arithmetic, not this file's: the
/// title bar, the tab strip and the page are placed from it, so the page cannot start inside the tabs. How
/// wide a control is comes from the control's own content (see <see cref="OnlineUiControlView"/>), and how
/// the parts INSIDE one of the game's rows sit is <see cref="OnlineUiRowGeometry"/>'s; an open dropdown's
/// list rides the window's own popup layer (<see cref="OnlineUiDropdownPopup"/>).
/// </para>
///
/// <para>
/// What the player does is reported as intents — a click on a control's id, a typed value, a chosen
/// option, a moved slider, the close control — and nothing here decides what any of it means. The pointer
/// over the window's own rect is POLLED and reported as a flip, for the same reason the launcher polls:
/// the uGUI callbacks fire on movement, and an in-world right-click is judged by where the pointer is.
/// </para>
/// </summary>
internal sealed class OnlineUiWindowView
{
	/// <summary>The window's size, from the Runtime's own shell layout (ticket
	/// online-ui-layout-and-input-detail-pass, S1: the frame the user asked to be larger, with the text and
	/// the controls inside it unchanged).</summary>
	internal const float Width = OnlineUiWindowLayout.Width;

	internal const float Height = OnlineUiWindowLayout.Height;

	internal const float Padding = OnlineUiWindowLayout.Padding;

	internal const float TitleHeight = OnlineUiWindowLayout.TitleHeight;

	internal const float TabHeight = OnlineUiWindowLayout.TabHeight;

	internal const float CloseWidth = 26f;

	internal const float CloseHeight = 22f;

	internal const float LineSpacing = OnlineUiWindowLayout.RowGap;

	internal const float GapHeight = OnlineUiWindowLayout.BlockGap;

	internal const string CloseCaption = "×";

	/// <summary>The content width the rows are wrapped against: the window minus its padding, which is also
	/// the viewport's width (the scroll has no scrollbar of its own).</summary>
	internal const float ContentWidth = Width - (2f * Padding);

	/// <summary>The window's own frame tint, applied to the game's sprite so the panel keeps the game's
	/// shape and CUO's dark surface.</summary>
	private static readonly Color PanelTint = new(0.035f, 0.045f, 0.06f, 0.97f);

	private static readonly Color TitleTint = new(0.07f, 0.09f, 0.12f, 0.98f);

	private readonly GameObject _root;
	private readonly RectTransform _rect;
	private readonly Canvas? _canvas;
	private readonly TextMeshProUGUI _title;
	private readonly RectTransform _tabRow;
	private readonly RectTransform _content;
	private readonly OnlineUiViewSource _views;
	private readonly OnlineUiDropdownPopup _popup;
	private readonly OnlineUiPageScrollbar _scrollbar;
	private readonly List<OnlineUiWindowRowView> _rows = [];
	private readonly List<OnlineUiControlView> _tabs = [];

	/// <summary>The width each element of a row was laid out at, kept per row so the page's column pass can
	/// lay every element out again without measuring anything twice.</summary>
	private readonly List<float[]> _widths = [];

	private bool _hovered;
	private bool _structureDirty;
	private Vector2 _fittedTo;

	private OnlineUiWindowView(
		ILogger log,
		GameObject root,
		RectTransform rect,
		Canvas? canvas,
		TextMeshProUGUI title,
		RectTransform tabRow,
		RectTransform content,
		Action<OnlineUiIntent> report,
		OnlineUiControlTypography typography,
		OnlineUiDropdownPopup popup,
		OnlineUiPageScrollbar scrollbar)
	{
		_views = new OnlineUiViewSource(log, "Online UI window", typography, report);
		_root = root;
		_rect = rect;
		_canvas = canvas;
		_title = title;
		_tabRow = tabRow;
		_content = content;
		_popup = popup;
		_scrollbar = scrollbar;
	}

	/// <summary>Builds the window under CUO's canvas. It starts hidden: a frame that carries a window model
	/// is what shows it.</summary>
	internal static OnlineUiWindowView Create(Transform parent, ILogger log, Action<OnlineUiIntent> report)
	{
		var root = new GameObject("CUO Online UI Window", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
		root.transform.SetParent(parent, worldPositionStays: false);
		var rect = (RectTransform)root.transform;
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = Vector2.zero;
		rect.sizeDelta = OnlineUiWindowPlacement.Fit(parent, new Vector2(Width, Height));
		rect.localScale = Vector3.one;

		// The shell's bands are CHILDREN of one layout group rather than three rects placed by hand: the title
		// bar, the tab strip and the page each declare their own height and the ENGINE stacks them, with the
		// layout's own spacing between them and the page taking whatever is left. The arithmetic that once put
		// the page inside the tab strip is gone with them — and so is the chance of a new band being forgotten
		// by it.
		var shell = root.GetComponent<VerticalLayoutGroup>();
		shell.padding = new RectOffset((int)Padding, (int)Padding, (int)Padding, (int)Padding);
		shell.spacing = OnlineUiWindowLayout.TabGap;
		shell.childAlignment = TextAnchor.UpperCenter;
		shell.childControlWidth = true;
		shell.childControlHeight = true;
		shell.childForceExpandWidth = true;
		shell.childForceExpandHeight = false;

		OnlineUiControlFactory.ReadRowTemplate(root.transform, out var typography, out var sprite, out var imageType, out var pixelsPerUnit);

		// The frame is the game's own window: its sprite UNTINTED carries the game's border — the first cut
		// tinted the whole frame dark and lost it, which is the border the acceptance pass missed — with the
		// dark surface the window reads on laid inside as a fill.
		OnlineUiControlFactory.MakeFrame(root, sprite, imageType, pixelsPerUnit, PanelTint);

		var panel = root.GetComponent<Image>();

		// The panel is a raycast target on purpose: a click inside the window must land on CUO's surface
		// and not fall through to the world behind it.
		panel.raycastTarget = true;

		var titleBar = OnlineUiSurfaceParts.CreateRect("Title Bar", root.transform, typeof(Image), typeof(LayoutElement));
		DeclareBandHeight(titleBar, TitleHeight);
		var titleImage = titleBar.GetComponent<Image>();
		titleImage.sprite = sprite;
		titleImage.type = imageType;
		titleImage.pixelsPerUnitMultiplier = pixelsPerUnit;
		titleImage.color = TitleTint;
		titleImage.raycastTarget = true;
		titleBar.gameObject.AddComponent<OnlineUiWindowDragHandler>().Bind(rect);

		var title = OnlineUiSurfaceParts.CreateLabel("Title", titleBar, typography, OnlineUiTextStyle.Title);
		OnlineUiSurfaceParts.Stretch(title.rectTransform, left: 8f, right: CloseWidth + 12f, top: 0f, bottom: 0f);
		title.alignment = TextAlignmentOptions.MidlineLeft;

		var close = OnlineUiControlView.Create(
			OnlineUiElementModel.Button(OnlineUiControlIds.WindowClose, CloseCaption, CloseWidth),
			titleBar,
			typography,
			report);
		var closeRect = (RectTransform)close.Root.transform;
		closeRect.anchorMin = new Vector2(1f, 0.5f);
		closeRect.anchorMax = new Vector2(1f, 0.5f);
		closeRect.pivot = new Vector2(1f, 0.5f);
		closeRect.anchoredPosition = new Vector2(-4f, 0f);
		closeRect.sizeDelta = new Vector2(CloseWidth, CloseHeight);

		var tabRow = OnlineUiSurfaceParts.CreateRect("Tabs", root.transform, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
		DeclareBandHeight(tabRow, TabHeight);
		var tabs = tabRow.GetComponent<HorizontalLayoutGroup>();
		tabs.spacing = LineSpacing;
		tabs.childAlignment = TextAnchor.MiddleLeft;
		tabs.childControlWidth = true;
		tabs.childControlHeight = true;
		tabs.childForceExpandWidth = false;
		tabs.childForceExpandHeight = true;

		var scroll = OnlineUiSurfaceParts.CreateRect("Scroll", root.transform, typeof(ScrollRect), typeof(LayoutElement));
		var scrollLayout = scroll.GetComponent<LayoutElement>();

		// The page is the band that takes what is left of the window: a flexible height, so the engine gives it
		// every unit the title bar and the tab strip did not claim.
		scrollLayout.flexibleHeight = 1f;
		var scrollRect = scroll.GetComponent<ScrollRect>();
		scrollRect.horizontal = false;
		scrollRect.vertical = true;
		scrollRect.movementType = ScrollRect.MovementType.Clamped;
		scrollRect.scrollSensitivity = 30f;

		var viewport = OnlineUiSurfaceParts.CreateRect("Viewport", scroll, typeof(Image), typeof(RectMask2D));
		OnlineUiSurfaceParts.Stretch(viewport, 0f, 0f, 0f, 0f);

		// The bar rides the PAGE's own band, beside the viewport rather than inside it: the mask clips what
		// is scrolled, and the one thing that must not scroll away is the bar that says how much is left
		// (ticket online-ui-paired-controls-and-page-scroll, S4).
		var scrollbar = OnlineUiPageScrollbar.Create(scroll, sprite, imageType, pixelsPerUnit);

		// The viewport's own transparent image catches the wheel over empty space, where no row is under
		// the pointer; the mask is what clips the rows to the viewport.
		var viewportImage = viewport.GetComponent<Image>();
		viewportImage.color = new Color(0f, 0f, 0f, 0f);
		viewportImage.raycastTarget = true;

		var content = OnlineUiSurfaceParts.CreateRect("Content", viewport, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
		content.anchorMin = new Vector2(0f, 1f);
		content.anchorMax = new Vector2(1f, 1f);
		content.pivot = new Vector2(0.5f, 1f);
		content.anchoredPosition = Vector2.zero;
		content.sizeDelta = Vector2.zero;
		var vertical = content.GetComponent<VerticalLayoutGroup>();
		vertical.spacing = LineSpacing;
		vertical.childAlignment = TextAnchor.UpperLeft;
		vertical.childControlWidth = true;
		vertical.childControlHeight = true;
		vertical.childForceExpandWidth = true;
		vertical.childForceExpandHeight = false;
		var fitter = content.GetComponent<ContentSizeFitter>();
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		// The page's own place in the shell is the engine's now (a flexible band under the tab strip, spaced by
		// the shell group): the arithmetic that once put its top edge inside the tab strip is gone, and with it
		// the class of defect the acceptance pass found there.
		scrollRect.viewport = viewport;
		scrollRect.content = content;

		// An open dropdown's list is an overlay: the layer takes the game's templates out of the page's mask
		// and layout, and sorts above CUO's own canvas, so the options are visible and clickable (S3).
		var popup = OnlineUiDropdownPopup.Create(rect, OnlineUiSurfaceHost.SortingOrder);

		var view = new OnlineUiWindowView(
			log,
			root,
			rect,
			root.GetComponentInParent<Canvas>()?.rootCanvas,
			title,
			tabRow,
			content,
			report,
			typography,
			popup,
			scrollbar);
		view.SetVisible(false);
		return view;
	}

	/// <summary>Shows or hides the whole window; a hidden window is still there, so reopening it does not
	/// rebuild its controls.</summary>
	internal void SetVisible(bool visible)
	{
		if (_root.activeSelf != visible)
		{
			_root.SetActive(visible);
		}
	}

	/// <summary>Applies one frame's model: the title, the tab row and the page's rows, each written only
	/// where it differs from what is already shown.</summary>
	internal void Apply(OnlineUiWindowModel model)
	{
		if (_title.text != model.Title)
		{
			_title.text = model.Title;
		}

		ApplyTabs(model.Tabs);
		ApplyRows(model.Rows);
		_popup.Normalise();
		_scrollbar.Apply();
		FitToCanvasIfItChanged();

		if (_structureDirty)
		{
			_structureDirty = false;
			Reorder();
		}
	}

	/// <summary>
	/// Polls the pointer against the window's rect and queues the hover fact when it flips. It runs even
	/// while the window is hidden, so closing it under the pointer still reports the pointer as out.
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
		intents.Enqueue(new OnlineUiIntent(
			hovered ? OnlineUiIntentKind.WindowHoverEntered : OnlineUiIntentKind.WindowHoverLeft));
	}

	internal void Destroy() => Object.Destroy(_root);

	/// <summary>
	/// Keeps the frame on the canvas it hangs on. The fit is measured once when the window is built, and a
	/// canvas that changes size afterwards (a resolution change, the game's own UI scale) would otherwise leave
	/// the frame hanging off it; the check is one rect comparison per frame the window is applied.
	/// </summary>
	private void FitToCanvasIfItChanged()
	{
		if (_canvas is null)
		{
			return;
		}

		var canvas = (RectTransform)_canvas.transform;
		if (canvas.rect.size == _fittedTo || canvas.rect.width <= 0f || canvas.rect.height <= 0f)
		{
			return;
		}

		_fittedTo = canvas.rect.size;
		_rect.sizeDelta = OnlineUiWindowPlacement.Fit(canvas, new Vector2(Width, Height));
	}

	/// <summary>
	/// Applies the strip's tabs, and then shares the strip's own width among them. A tab is an element that IS
	/// its own control, so the width its model declares is a FLOOR like every other width of this surface
	/// (<see cref="OnlineUiPageBuilder.TabWidth"/>: six of them on one line) and above that floor it takes its
	/// own caption — but the strip is a band of a fixed width, so the widths the tabs actually take are the
	/// Runtime rule's answer (<see cref="OnlineUiTabLayout"/>) over the least each caption can be drawn at.
	/// Handing every tab what it asked for is what measured the six English tabs at 1038.7 units inside a
	/// 984-unit page, with the last one 55 units past the window's right edge (the user's report of 2026-10-14,
	/// batch `20261015-a`).
	/// </summary>
	private void ApplyTabs(IReadOnlyList<OnlineUiElementModel> tabs)
	{
		// The strip's views are reconciled BEFORE their widths are read, because what a tab asks for is its
		// caption at the size it now draws at.
		_structureDirty |= _views.Sync(_tabs, tabs, _tabRow);

		for (var index = 0; index < tabs.Count; index++)
		{
			_tabs[index].Apply(tabs[index]);
		}

		var natural = new float[tabs.Count];
		var minimum = new float[tabs.Count];
		for (var index = 0; index < tabs.Count; index++)
		{
			natural[index] = _tabs[index].EffectiveWidth(tabs[index]);
			minimum[index] = _tabs[index].MinimumWidth(tabs[index]);
		}

		var widths = OnlineUiTabLayout.Widths(natural, minimum, TabStripWidth, LineSpacing);
		for (var index = 0; index < tabs.Count; index++)
		{
			_tabs[index].LayOutRow(tabs[index], widths[index], columnWidth: 0f);
		}
	}

	/// <summary>
	/// The width the tab strip has to share among its tabs: the window's own inner width, which is the band the
	/// shell's vertical group has to give and cannot be moved by what it is given TO.
	///
	/// <para>
	/// The band's own rect is NOT the source, and this is the one mistake the first cut of this rule made: the
	/// band is a child of the shell's group with <c>childForceExpandWidth</c>, so its rect is the group's inner
	/// width OR its own children's minimum, whichever is larger — and a tab's minimum is the width this very
	/// rule just wrote on it (`OnlineUiRowGeometry.LayOutRow` writes <c>minWidth</c> and <c>preferredWidth</c>
	/// together). A budget read off the band therefore equals exactly what the tabs already asked for and the
	/// strip can never give anything up: the live reading is the proof, `Tabs rect=1038.7x36.0` in the English
	/// dump where the six tabs sum to 1038.8, against `Title Bar rect=984.0x28.0` on the same shell (batch
	/// `20261015-a`). That is the same self-measurement `OnlineUiControlSizing` records from batch `20261001-k`,
	/// in a form that converges instead of running away.
	/// </para>
	///
	/// <para>
	/// The window's rect is the frame's own — the surface host and the canvas clamp write it, and no tab feeds
	/// into it — so the budget follows a clamped window (a smaller canvas is a narrower strip) exactly as
	/// intended. The declared content width stands in for a frame whose rect is still zero, before its first
	/// layout pass.
	/// </para>
	/// </summary>
	private float TabStripWidth => _rect.rect.width > 0f ? _rect.rect.width - (2f * Padding) : ContentWidth;

	/// <summary>
	/// Applies the page's rows, one row at a time. The control column they share is decided AFTER the models
	/// are applied and from what the views now show — a name is the one width of this surface that does not
	/// depend on the box it is drawn in — and it is written back in one extra pass, so the alignment cannot
	/// feed itself the way a rule that measured a group's own output would.
	/// </summary>
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
	/// The width one element's NAME needs on this surface: the view the row already holds for it — built
	/// before this runs, so it carries the game's own font and the text the model put there — measured on its
	/// label (<see cref="OnlineUiControlView.NameWidthOf"/>). Nothing is built and no box is asked, so the
	/// column cannot be read back out of the layout it is about to write.
	/// </summary>
	private float NameWidth(int rowIndex, int index, OnlineUiElementModel element) =>
		rowIndex < _rows.Count && index < _rows[rowIndex].Elements.Count
			? _rows[rowIndex].Elements[index].NameWidthOf(element)
			: 0f;

	/// <summary>
	/// One model row on the surface, at its own index in the page: the views, the wrap (the fit and the
	/// model's own breaks together — <see cref="OnlineUiRowLayout.LineOf(IReadOnlyList{float}, IReadOnlyList{bool}, float, float)"/>),
	/// the lines that wrap produced, and every element placed on the line it landed on. The widths it
	/// computed are kept for the page's column pass, which runs once every row of the page has its own.
	/// </summary>
	private void ApplyRow(int rowIndex, OnlineUiWindowRowView row, OnlineUiRowModel model)
	{
		var elements = model.Elements;
		if (elements.Count == 0)
		{
			// An empty row is the page's room: one line with nothing in it but the room the model asked for —
			// a heading's own block room, or the gap between two blocks. A model that names no room keeps the
			// layout's own block gap.
			_structureDirty |= row.EnsureLines(_content, 1);
			row.SetGap(0, model.Gap > 0f ? model.Gap : GapHeight);
			_structureDirty |= row.DestroyElementsFrom(0);
			_widths[rowIndex] = [];
			return;
		}

		// The views come first, and the wrap comes after them: how wide a control is comes from its own
		// content — the game's font at the game's size decides what a caption needs — so the model's width is
		// only a floor and the lines are decided from what the controls actually take.
		_structureDirty |= _views.Sync(row.Elements, elements, _content);

		var widths = new float[elements.Count];
		var breaks = new bool[elements.Count];
		for (var index = 0; index < elements.Count; index++)
		{
			var view = row.Elements[index];
			view.Apply(elements[index], aloneOnItsLine: elements.Count == 1);
			widths[index] = view.EffectiveWidth(elements[index]);
			breaks[index] = elements[index].NewLine;
		}

		var lines = OnlineUiRowLayout.LineOf(widths, breaks, ContentWidth, LineSpacing);
		_widths[rowIndex] = widths;
		var lineCount = lines[lines.Length - 1] + 1;
		_structureDirty |= row.EnsureLines(_content, lineCount);
		for (var index = 0; index < lineCount; index++)
		{
			row.SetGap(index, -1f);
		}

		for (var index = 0; index < elements.Count; index++)
		{
			var view = row.Elements[index];
			view.SetParent(row.Lines[lines[index]].transform);

			// A row that carries its own label and control (a dropdown, a field, a slider, a toggle) is laid
			// out by CUO inside: the game's own placement belongs to the game's own screen. The width it takes
			// on its line is the one just computed — the width CUO declared, never a measurement of the
			// control's own stretched content. A paired element is stacked instead, and takes the column when
			// the page's own pass runs.
			view.LayOutRow(elements[index], widths[index], columnWidth: 0f);

			// An open dropdown's list is an overlay, and the window owns the layer it has to live on.
			view.AdoptPopup(_popup);
		}

		_structureDirty |= row.DestroyElementsFrom(elements.Count);
	}

	/// <summary>Puts the lines back in model order. Unity appends a new object to the end of its parent, so
	/// a row that gained a line would otherwise draw below every row after it.</summary>
	private void Reorder()
	{
		foreach (var row in _rows)
		{
			foreach (var line in row.Lines)
			{
				line.transform.SetAsLastSibling();
			}
		}
	}

	/// <summary>Declares one band's own height for the shell's layout group: the engine places the band, this is
	/// the size the band asks to be — the only thing CUO says about the shell's stacking.</summary>
	private static void DeclareBandHeight(RectTransform band, float height)
	{
		var element = band.GetComponent<LayoutElement>();
		element.minHeight = height;
		element.preferredHeight = height;

		// Only the PAGE takes the window's leftover height (its own flexible band is set up below). Without
		// this zero the tab strip's inner layout group reported its children's flexible sum, so the shell fed
		// the strip the leftover height and it grew to 333 units — ten times TabHeight (user report,
		// 2026-09-27).
		element.flexibleHeight = 0f;
	}
}
