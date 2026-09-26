using System;
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
/// The CUO Online UI window's shell on the game's own canvas (ticket online-ui-art-and-controls-overhaul,
/// S2b): the frame, the title bar the player drags, the close control, the tab row and the scrolling page
/// of the game's own rows.
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
/// The frame's own art is the game's: the sprite, its <c>Image.type</c> and its pixels-per-unit
/// multiplier are read from the game's own row prefab (the same one the launcher is built from), and the
/// typography of every label this surface creates comes from that prefab's label. The tint is CUO's, and
/// it is the only colour the surface owns: every content colour travels in the model.
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
	/// <summary>The window's size: the rect the IMGUI window occupied, now in the game's canvas units.</summary>
	internal const float Width = 780f;

	internal const float Height = 540f;

	internal const float Padding = 8f;

	internal const float TitleHeight = 28f;

	internal const float TabHeight = 30f;

	internal const float CloseWidth = 26f;

	internal const float CloseHeight = 22f;

	internal const float LineSpacing = 4f;

	internal const float GapHeight = 10f;

	internal const string CloseCaption = "×";

	/// <summary>The content width the rows are wrapped against: the window minus its padding, which is also
	/// the viewport's width (the scroll has no scrollbar of its own).</summary>
	internal const float ContentWidth = Width - (2f * Padding);

	/// <summary>The window's own frame tint, applied to the game's sprite so the panel keeps the game's
	/// shape and CUO's dark surface.</summary>
	private static readonly Color PanelTint = new(0.035f, 0.045f, 0.06f, 0.97f);

	private static readonly Color TitleTint = new(0.07f, 0.09f, 0.12f, 0.98f);

	private readonly ILogger _log;
	private readonly GameObject _root;
	private readonly RectTransform _rect;
	private readonly Canvas? _canvas;
	private readonly TextMeshProUGUI _title;
	private readonly RectTransform _tabRow;
	private readonly RectTransform _content;
	private readonly Action<OnlineUiIntent> _report;
	private readonly OnlineUiControlView.Typography _typography;
	private readonly List<OnlineUiWindowRowView> _rows = [];
	private readonly List<OnlineUiControlView> _tabs = [];
	private readonly HashSet<OnlineUiElementKind> _reportedMissingPrefabs = [];

	private bool _hovered;
	private bool _structureDirty;

	private OnlineUiWindowView(
		ILogger log,
		GameObject root,
		RectTransform rect,
		Canvas? canvas,
		TextMeshProUGUI title,
		RectTransform tabRow,
		RectTransform content,
		Action<OnlineUiIntent> report,
		OnlineUiControlView.Typography typography)
	{
		_log = log;
		_root = root;
		_rect = rect;
		_canvas = canvas;
		_title = title;
		_tabRow = tabRow;
		_content = content;
		_report = report;
		_typography = typography;
	}

	/// <summary>Builds the window under CUO's canvas. It starts hidden: a frame that carries a window model
	/// is what shows it.</summary>
	internal static OnlineUiWindowView Create(Transform parent, ILogger log, Action<OnlineUiIntent> report)
	{
		var root = new GameObject("CUO Online UI Window", typeof(RectTransform), typeof(Image));
		root.transform.SetParent(parent, worldPositionStays: false);
		var rect = (RectTransform)root.transform;
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = Vector2.zero;
		rect.sizeDelta = new Vector2(Width, Height);
		rect.localScale = Vector3.one;

		ReadGameRowTemplate(root.transform, out var typography, out var sprite, out var imageType, out var pixelsPerUnit);
		var panel = root.GetComponent<Image>();
		panel.sprite = sprite;
		panel.type = imageType;
		panel.pixelsPerUnitMultiplier = pixelsPerUnit;
		panel.color = PanelTint;

		// The panel is a raycast target on purpose: a click inside the window must land on CUO's surface
		// and not fall through to the world behind it.
		panel.raycastTarget = true;

		var titleBar = CreateRect("Title Bar", root.transform, typeof(Image));
		StretchTop(titleBar, Padding, TitleHeight);
		var titleImage = titleBar.GetComponent<Image>();
		titleImage.sprite = sprite;
		titleImage.type = imageType;
		titleImage.pixelsPerUnitMultiplier = pixelsPerUnit;
		titleImage.color = TitleTint;
		titleImage.raycastTarget = true;
		titleBar.gameObject.AddComponent<OnlineUiWindowDragHandler>().Bind(rect);

		var title = CreateLabel("Title", titleBar, typography, OnlineUiTextStyle.Title);
		Stretch(title.rectTransform, left: 8f, right: CloseWidth + 12f, top: 0f, bottom: 0f);
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

		var tabRow = CreateRect("Tabs", root.transform, typeof(HorizontalLayoutGroup));
		StretchTop(tabRow, Padding, TabHeight, TitleHeight + LineSpacing);
		var tabs = tabRow.GetComponent<HorizontalLayoutGroup>();
		tabs.spacing = LineSpacing;
		tabs.childAlignment = TextAnchor.MiddleLeft;
		tabs.childControlWidth = true;
		tabs.childControlHeight = true;
		tabs.childForceExpandWidth = false;
		tabs.childForceExpandHeight = true;

		var scroll = CreateRect("Scroll", root.transform, typeof(ScrollRect));
		var scrollRect = scroll.GetComponent<ScrollRect>();
		scrollRect.horizontal = false;
		scrollRect.vertical = true;
		scrollRect.movementType = ScrollRect.MovementType.Clamped;
		scrollRect.scrollSensitivity = 30f;

		var viewport = CreateRect("Viewport", scroll, typeof(Image), typeof(RectMask2D));
		Stretch(viewport, 0f, 0f, 0f, 0f);

		// The viewport's own transparent image catches the wheel over empty space, where no row is under
		// the pointer; the mask is what clips the rows to the viewport.
		var viewportImage = viewport.GetComponent<Image>();
		viewportImage.color = new Color(0f, 0f, 0f, 0f);
		viewportImage.raycastTarget = true;

		var content = CreateRect("Content", viewport, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
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

		scrollRect.viewport = viewport;
		scrollRect.content = content;

		// The area below the tabs belongs to the scroll: the same padding on three sides as the tabs have.
		Stretch(scroll, Padding, Padding, TitleHeight + TabHeight + (2f * LineSpacing), Padding);

		var view = new OnlineUiWindowView(
			log,
			root,
			rect,
			root.GetComponentInParent<Canvas>()?.rootCanvas,
			title,
			tabRow,
			content,
			report,
			typography);
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

	private void ApplyTabs(IReadOnlyList<OnlineUiElementModel> tabs)
	{
		for (var index = 0; index < tabs.Count; index++)
		{
			var element = tabs[index];
			var view = index < _tabs.Count && Matches(_tabs[index], element)
				? _tabs[index]
				: Replace(_tabs, index, element, _tabRow);
			view.Apply(element);
		}

		DestroyFrom(_tabs, tabs.Count);
	}

	private void ApplyRows(IReadOnlyList<OnlineUiRowModel> rows)
	{
		while (_rows.Count > rows.Count)
		{
			_rows[_rows.Count - 1].Destroy();
			_rows.RemoveAt(_rows.Count - 1);
			_structureDirty = true;
		}

		for (var index = 0; index < rows.Count; index++)
		{
			if (index >= _rows.Count)
			{
				_rows.Add(new OnlineUiWindowRowView(LineSpacing));
				_structureDirty = true;
			}

			ApplyRow(_rows[index], rows[index]);
		}
	}

	private void ApplyRow(OnlineUiWindowRowView row, OnlineUiRowModel model)
	{
		var elements = model.Elements;
		if (elements.Count == 0)
		{
			// An empty row is the page's breathing room: one line with nothing in it but a height.
			_structureDirty |= row.EnsureLines(_content, 1);
			row.SetGap(0, GapHeight);
			_structureDirty |= row.DestroyElementsFrom(0);
			return;
		}

		var widths = new float[elements.Count];
		for (var index = 0; index < elements.Count; index++)
		{
			widths[index] = elements[index].Width;
		}

		var lines = OnlineUiRowLayout.LineOf(widths, ContentWidth, LineSpacing);
		var lineCount = lines[lines.Length - 1] + 1;
		_structureDirty |= row.EnsureLines(_content, lineCount);
		for (var index = 0; index < lineCount; index++)
		{
			row.SetGap(index, -1f);
		}

		for (var index = 0; index < elements.Count; index++)
		{
			var element = elements[index];
			var view = index < row.Elements.Count && Matches(row.Elements[index], element)
				? row.Elements[index]
				: Replace(row.Elements, index, element, row.Lines[lines[index]].transform);

			view.SetParent(row.Lines[lines[index]].transform);
			view.Apply(element);
		}

		_structureDirty |= row.DestroyElementsFrom(elements.Count);
	}

	/// <summary>Whether a live view can take the element over: same kind, and — for a control the model
	/// names — the same id. A control that changed identity is rebuilt, because its click must land on the
	/// action its id was registered under.</summary>
	private static bool Matches(OnlineUiControlView view, OnlineUiElementModel element) =>
		view.Kind == element.Kind
		&& (element.Id.Length == 0 || string.Equals(view.Id, element.Id, StringComparison.Ordinal));

	/// <summary>
	/// Builds one element's view, replacing whatever occupied its slot. Creation cannot fail: a control the
	/// game no longer ships falls back to a placeholder inside the view itself, so the slot always holds a
	/// view and the model's positions stay aligned with the list.
	/// </summary>
	private OnlineUiControlView Replace(
		List<OnlineUiControlView> views,
		int index,
		OnlineUiElementModel element,
		Transform parent)
	{
		if (index < views.Count)
		{
			views[index].Destroy();
			views.RemoveAt(index);
		}

		_structureDirty = true;
		var view = OnlineUiControlView.Create(element, parent, _typography, _report);
		if (view.MissedGamePrefab && _reportedMissingPrefabs.Add(element.Kind))
		{
			_log.LogWarning(
				"Online UI window: the game's own row prefab for {Kind} could not be loaded — that control falls back to a plain placeholder.",
				element.Kind);
		}

		views.Insert(index, view);
		return view;
	}

	private static void DestroyFrom(List<OnlineUiControlView> views, int keep)
	{
		if (views.Count <= keep)
		{
			return;
		}

		for (var index = keep; index < views.Count; index++)
		{
			views[index].Destroy();
		}

		views.RemoveRange(keep, views.Count - keep);
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

	private static RectTransform CreateRect(string name, Transform parent, params Type[] components)
	{
		var all = new Type[components.Length + 1];
		all[0] = typeof(RectTransform);
		Array.Copy(components, 0, all, 1, components.Length);
		var go = new GameObject(name, all);
		go.transform.SetParent(parent, worldPositionStays: false);
		return (RectTransform)go.transform;
	}

	private static TextMeshProUGUI CreateLabel(
		string name,
		Transform parent,
		OnlineUiControlView.Typography typography,
		OnlineUiTextStyle style)
	{
		var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
		go.transform.SetParent(parent, worldPositionStays: false);
		var text = go.GetComponent<TextMeshProUGUI>();
		text.fontSize = typography.SizeOf(style);
		text.fontStyle = OnlineUiControlView.Typography.WeightOf(style);
		text.raycastTarget = false;
		if (typography.Font != null)
		{
			text.font = typography.Font;
		}

		return text;
	}

	/// <summary>Stretches a rect inside its parent, with the given insets from each edge.</summary>
	private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.offsetMin = new Vector2(left, bottom);
		rect.offsetMax = new Vector2(-right, -top);
	}

	/// <summary>Pins a rect to the top of its parent, <paramref name="offset"/> below the previous one.</summary>
	private static void StretchTop(RectTransform rect, float inset, float height, float offset = 0f)
	{
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.offsetMin = new Vector2(inset, -(inset + offset + height));
		rect.offsetMax = new Vector2(-inset, -(inset + offset));
	}

	/// <summary>
	/// Reads what the game's own row prefab carries — the font and size of its label, and the sprite, type
	/// and pixels-per-unit multiplier of its background — so the window's labels and frame are the game's
	/// rather than a guess. The template instance is destroyed in the same call: it exists to be read.
	/// </summary>
	private static void ReadGameRowTemplate(
		Transform parent,
		out OnlineUiControlView.Typography typography,
		out Sprite? sprite,
		out Image.Type imageType,
		out float pixelsPerUnit)
	{
		const float fallbackSize = 14f;
		sprite = null;
		imageType = Image.Type.Simple;
		pixelsPerUnit = 1f;

		var prefab = Resources.Load<GameObject>(OnlineUiControlFactory.ButtonRowPrefabPath);
		if (prefab is null)
		{
			typography = new OnlineUiControlView.Typography(TMP_Settings.defaultFontAsset, fallbackSize);
			return;
		}

		var probe = Object.Instantiate(prefab, parent);
		probe.SetActive(false);
		var label = probe.transform.childCount > 0 ? probe.transform.GetChild(0).GetComponent<TextMeshProUGUI>() : null;
		var image = probe.GetComponent<Image>();
		var font = label != null && label.font != null ? label.font : TMP_Settings.defaultFontAsset;
		var size = label != null && label.fontSize > 0f ? label.fontSize : fallbackSize;
		typography = new OnlineUiControlView.Typography(font, size);
		if (image != null)
		{
			sprite = image.sprite;
			imageType = image.type;
			pixelsPerUnit = image.pixelsPerUnitMultiplier;
		}

		Object.Destroy(probe);
	}

}
