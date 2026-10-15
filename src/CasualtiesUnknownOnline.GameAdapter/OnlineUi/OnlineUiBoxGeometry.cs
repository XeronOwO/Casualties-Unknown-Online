using CasualtiesUnknownOnline.Runtime.OnlineUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// The inside of one control that shows a single BOX — a field, a dropdown, a checkbox (ticket
/// online-ui-layout-and-input-detail-pass, S2, split out of <see cref="OnlineUiRowGeometry"/> by the cycle of
/// 2026-10-15): the game's own boxes inside it are hidden, CUO's one box takes their place, and every part
/// the game's row placed by hand is placed again from the box's own numbers.
///
/// <para>
/// It is a type of its own because it is a rule about ONE box rather than about a row: the row decides where
/// its label and its control sit and how wide the control is (<see cref="OnlineUiRowGeometry"/>), while this
/// decides what a control that shows a value looks like from the inside. A slider is the third case and takes
/// neither — its own parts ARE its reading (<see cref="OnlineUiSliderGeometry"/>).
/// </para>
///
/// <para>
/// Every rect here is computed from the control's own width or from a child's own content, never from a rect a
/// layout group just wrote, so the pass is idempotent and cannot feed itself — the runaway of batch
/// `20261001-k`, where a dropdown reached 690,000 units wide inside a 984-unit page.
/// </para>
/// </summary>
internal static class OnlineUiBoxGeometry
{
	/// <summary>How much of a control's box its content keeps clear: the room the control's own art (its
	/// arrow, its frame) needs on the inside.</summary>
	internal const float InnerPadding = 10f;

	/// <summary>The room a control's content keeps from the TOP and BOTTOM of its box. It is half the inner
	/// padding because a control of this page is one compact height (36) rather than the height the game's own
	/// screen authored — a field whose text area kept a 10-unit inset at each edge in a 36-unit box was 16 units
	/// tall, too short for the font it draws (the live reading in batch `20261001-k`).</summary>
	private const float VerticalInset = InnerPadding * 0.5f;

	/// <summary>The width under which a child of a control is read as the game's own art (a dropdown's arrow)
	/// rather than as content: a control of this surface is one compact height tall, and nothing the game draws
	/// as decoration is wider than that.</summary>
	private const float LargestAccessoryWidth = OnlineUiWindowLayout.ControlHeight;

	/// <summary>The four hairlines CUO draws inside a control's own edge, in the order top, bottom, left, right.
	/// They are the ONE box the control shows: the game's own boxes inside it are hidden.</summary>
	private static readonly string[] EdgeNames = ["CUO Control Edge Top", "CUO Control Edge Bottom", "CUO Control Edge Left", "CUO Control Edge Right"];

	/// <summary>How thick a control's hairline is, in the canvas units the whole layout is in: thin enough to read
	/// as an edge rather than a frame, and one unit is the same edge the game's own sprite carries.</summary>
	private const float EdgeThickness = 1.5f;

	/// <summary>The colour of a control's edge: the light tone the game's own panels and captions use, so a control
	/// that holds a value reads as part of the same family.</summary>
	private static Color EdgeColor => new(1f, 1f, 1f, 0.55f);

	/// <summary>The fill one control that holds a value is drawn on, in the engine's own colour type.</summary>
	private static Color ControlFill => new(
		OnlineUiWindowLayout.ControlFillR,
		OnlineUiWindowLayout.ControlFillG,
		OnlineUiWindowLayout.ControlFillB,
		OnlineUiWindowLayout.ControlFillA);

	/// <summary>
	/// Places the children of a control that shows ONE box on the box it was given, one rect at a time.
	/// Everything the game's row placed by hand is placed again from the box's own numbers: a container (a
	/// field's text area, its placeholder, its caret) fills the box less the room the game's row leaves at its
	/// edges, a caption keeps the width its own text needs and starts at the left edge (so it never runs under
	/// the row's art), and a piece of art (a dropdown's arrow) keeps its own size at the right edge.
	///
	/// <para>
	/// A layout group or a fitter still sitting on one of these children would fight these writes, so those go.
	/// </para>
	/// </summary>
	internal static void LayOutInterior(RectTransform control, float controlWidth)
	{
		var interior = OnlineUiControlBox.InteriorWidth(controlWidth, InnerPadding);
		ApplyBoxStyle(control);

		// The room a caption keeps clear of: read BEFORE anything is re-placed, because that is where the
		// game's own prefab still says how wide the art and the boxes beside it were authored to be.
		var captionRoom = interior;
		for (var index = 0; index < control.childCount; index++)
		{
			if (control.GetChild(index) is RectTransform part && IsContainer(part))
			{
				captionRoom = Mathf.Max(0f, captionRoom - Mathf.Min(AuthoredWidthOf(part), interior));
			}
		}

		for (var index = 0; index < control.childCount; index++)
		{
			if (control.GetChild(index) is not RectTransform child)
			{
				continue;
			}

			if (child.GetComponent<OnlineUiControlEdge>() != null)
			{
				// CUO's own hairline: it belongs to the control's EDGE — <see cref="ApplyBoxStyle"/> places it —
				// and must not be stretched onto the content box or have its drawing hidden with the prefab's.
				continue;
			}

			var container = IsContainer(child);
			var authored = AuthoredWidthOf(child);
			DropLayoutDrivers(child);
			HidePrefabBoxArt(child);

			if (!container && child.GetComponent<TMP_Text>() is { } text)
			{
				LayOutTextAtLeft(child, TextWidthOf(text), captionRoom);
				continue;
			}

			if (!container && authored >= 0f && authored < LargestAccessoryWidth)
			{
				// A leaf narrower than a control's own height is the game's own art (a dropdown's arrow): it
				// keeps the size the prefab authored for it at the right-hand edge instead of being stretched
				// with the viewport, which is what the game's own row does with it.
				PinInside(child, InnerPadding, authored, fromTheRight: true);
				continue;
			}

			StretchInside(child, InnerPadding, InnerPadding);
		}
	}

	/// <summary>
	/// Gives a control that holds a value the ONE box it shows: a fill in the panel's own dark tint and a hairline
	/// inside its edge. Every write here is idempotent, so it runs in the same frame-by-frame pass as the rest.
	///
	/// <para>
	/// The hairline is four thin lines rather than the game's nine-slice sprite. That sprite's BODY is opaque —
	/// the window's own frame proves it: it is that sprite left untinted, and it draws a light panel — so a copy of
	/// it inside a control covers the dark fill the user asked for, which is exactly what the first run of this
	/// style did (`style1-preferences.png`: a white box with no text). Lines are the drawing the style needs, and
	/// a rect is what this pass already owns.
	/// </para>
	/// </summary>
	private static void ApplyBoxStyle(RectTransform control)
	{
		var fill = control.GetComponent<Image>();
		fill ??= control.gameObject.AddComponent<Image>();

		fill.color = ControlFill;
		fill.raycastTarget = true;

		foreach (var name in EdgeNames)
		{
			if (control.Find(name) is null)
			{
				var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(OnlineUiControlEdge));
				go.transform.SetParent(control, worldPositionStays: false);
				var line = go.GetComponent<Image>();
				line.color = EdgeColor;
				line.raycastTarget = false;
			}
		}

		PinEdge(control, EdgeNames[0], anchoredToTop: true, vertical: false);
		PinEdge(control, EdgeNames[1], anchoredToTop: false, vertical: false);
		PinEdge(control, EdgeNames[2], anchoredToTop: true, vertical: true);
		PinEdge(control, EdgeNames[3], anchoredToTop: false, vertical: true);
	}

	/// <summary>Places one hairline of a control's edge: a horizontal line spans its width, a vertical one its
	/// height, and both are <see cref="EdgeThickness"/> thick on the control's own edge.</summary>
	private static void PinEdge(RectTransform control, string name, bool anchoredToTop, bool vertical)
	{
		if (control.Find(name) is not RectTransform line)
		{
			return;
		}

		var side = anchoredToTop ? 1f : 0f;
		line.anchorMin = vertical ? new Vector2(side, 0f) : new Vector2(0f, side);
		line.anchorMax = vertical ? new Vector2(side, 1f) : new Vector2(1f, side);
		line.pivot = new Vector2(0.5f, 0.5f);
		line.sizeDelta = vertical ? new Vector2(EdgeThickness, 0f) : new Vector2(0f, EdgeThickness);
		line.offsetMin = vertical
			? new Vector2(line.offsetMin.x, 0f)
			: new Vector2(0f, line.offsetMin.y);
		line.offsetMax = vertical
			? new Vector2(line.offsetMax.x, 0f)
			: new Vector2(0f, line.offsetMax.y);
	}

	/// <summary>
	/// Hides the box the game's own row draws for this child. The user's report of 2026-10-01 is that a control
	/// showing two boxes — the prefab's own frame and CUO's — reads as a box inside a box with a margin nobody
	/// chose; the child keeps its rect and its raycast, and only its DRAWING goes. A text is left alone: it is what
	/// the player reads. The write is skipped when the graphic is already clear, because this runs once per frame
	/// per child and a colour write marks the graphic dirty.
	/// </summary>
	internal static void HidePrefabBoxArt(RectTransform child)
	{
		if (child.GetComponent<TMP_Text>() != null)
		{
			return;
		}

		if (child.GetComponent<Image>() is { } image && image.color.a > 0f)
		{
			image.color = new Color(0f, 0f, 0f, 0f);
		}

		if (child.GetComponent<RawImage>() is { } raw && raw.color.a > 0f)
		{
			raw.color = new Color(0f, 0f, 0f, 0f);
		}
	}

	/// <summary>
	/// Whether a control's child is a box that holds something of its own (a text field's viewport, its text
	/// area, a dropdown's value box) rather than one caption: a container fills the control's box, and a caption
	/// keeps the width its own text needs. The rule is the child's own content — a child that carries children
	/// is a box, and a text field is a box even when the game shipped it empty.
	/// </summary>
	private static bool IsContainer(RectTransform child) =>
		child.childCount > 0 || child.GetComponent<TMP_InputField>() != null;

	/// <summary>
	/// How wide the game's own prefab authored one of these children, read before CUO re-places any of them: an
	/// image measures its sprite and a layout element reports what the prefab declared. Zero when the child
	/// declares nothing, which the caller reads as "this one is content, fill the box with it".
	/// </summary>
	private static float AuthoredWidthOf(RectTransform child) =>
		LayoutUtility.GetPreferredSize(child, 0);

	/// <summary>The width one caption's own text needs. A value is one line (a dropdown's is forced to one), so
	/// the line break between two lines of the same caption counts as no width at all.</summary>
	private static float TextWidthOf(TMP_Text text)
	{
		var values = text.GetPreferredValues();
		return values.x > 0f ? values.x : 0f;
	}

	/// <summary>A caption at the left edge of the box: its own width, its own vertical place, and the room the
	/// game's row leaves on the left.</summary>
	private static void LayOutTextAtLeft(RectTransform text, float width, float interior)
	{
		var min = text.anchorMin;
		var max = text.anchorMax;
		min.x = 0f;
		max.x = 0f;
		text.anchorMin = min;
		text.anchorMax = max;
		text.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, InnerPadding, Mathf.Min(width, interior));
	}

	/// <summary>
	/// Pins one of a control's children inside the control's own box: the width is the one the game's prefab
	/// authored for it, the horizontal distance is measured from the given edge, and its height fills the box
	/// less the room the row keeps at its edges — the game's own rows are authored for a screen three times this
	/// height, so a child that keeps its authored height overflows the compact control it now lives in.
	/// </summary>
	private static void PinInside(RectTransform child, float distance, float width, bool fromTheRight)
	{
		var edge = fromTheRight ? 1f : 0f;
		var min = child.anchorMin;
		var max = child.anchorMax;
		min.x = edge;
		max.x = edge;
		child.anchorMin = min;
		child.anchorMax = max;
		child.SetInsetAndSizeFromParentEdge(
			fromTheRight ? RectTransform.Edge.Right : RectTransform.Edge.Left,
			distance,
			width);
		child.offsetMin = new Vector2(child.offsetMin.x, VerticalInset);
		child.offsetMax = new Vector2(child.offsetMax.x, -VerticalInset);
	}

	/// <summary>
	/// Fills one of a control's children onto the box the control was given, less the room the game's own row
	/// keeps at its edges, on both axes: that is what puts a field's text, its caret and its placeholder inside
	/// the frame the player sees.
	/// </summary>
	private static void StretchInside(RectTransform child, float left, float right)
	{
		var min = child.anchorMin;
		var max = child.anchorMax;
		min.x = 0f;
		max.x = 1f;
		child.anchorMin = min;
		child.anchorMax = max;
		child.offsetMin = new Vector2(left, left);
		child.offsetMax = new Vector2(-right, -right);
	}

	/// <summary>
	/// Takes the layout drivers off a child of a control whose own geometry CUO now writes by hand: a layout
	/// group asked to size these children measures them from their current rects, which is exactly the feedback
	/// this pass removed — the child's rects are the layout here.
	/// </summary>
	private static void DropLayoutDrivers(RectTransform child)
	{
		foreach (var group in child.GetComponents<LayoutGroup>())
		{
			Object.Destroy(group);
		}

		foreach (var fitter in child.GetComponents<ContentSizeFitter>())
		{
			Object.Destroy(fitter);
		}
	}
}
