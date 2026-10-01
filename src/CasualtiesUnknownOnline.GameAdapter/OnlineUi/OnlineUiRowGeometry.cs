using CasualtiesUnknownOnline.Runtime.OnlineUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// The geometry INSIDE one of the game's row prefabs (ticket online-ui-layout-and-input-detail-pass, S2,
/// reworked by its own acceptance pass). The game's settings screen places its rows' children by hand for its
/// own full-width panel, so a row instantiated into CUO's window keeps those authored rects: a dropdown's box,
/// a field's viewport or a label's text then sits at a position the window never sized, which is the user's
/// report of controls that overlap their neighbour, text clipped mid-word and boxes that cannot be clicked.
///
/// <para>
/// The row's own label and control are still laid out by a layout group on the row — the label flexible, the
/// control at the width the model declared (<see cref="OnlineUiControlSizing"/>) — but the control's WIDTH is
/// no longer measured from its own content. uGUI measures a stretched child by the rect it already has, so a
/// group asked for the preferred size of a field's text area reads the width the group itself just wrote: the
/// feedback ran away in the acceptance pass of batch `20261001-k`, where a dropdown reached 690,000 units wide
/// inside a 984-unit page while the value it held stayed correct. The width is declared, and the control's
/// INSIDES are then placed by CUO one rect at a time — the part a layout group cannot do for content the game
/// authored for a screen three times this size.
/// </para>
///
/// <para>
/// Everything here is idempotent: it is written for a view that is applied frame after frame, and a part the
/// prefab does not carry (a field with no viewport, a row with a single child) is simply skipped.
/// </para>
/// </summary>
internal static class OnlineUiRowGeometry
{
	/// <summary>The room a row's parts keep from the row's own edges.</summary>
	internal const float RowPadding = 6f;

	/// <summary>The room between a row's label and its control.</summary>
	internal const float RowGap = 8f;

	/// <summary>How much of a row's label a dropdown's caption or a field's text keeps clear: the room the
	/// control's own art (its arrow, its frame) needs on the inside.</summary>
	internal const float ControlInnerPadding = 10f;

	/// <summary>The room a control's content keeps from the TOP and BOTTOM of its box. It is half the inner
	/// padding because a control of this page is one compact height (36) rather than the height the game's own
	/// screen authored — a field whose text area kept a 10-unit inset at each edge in a 36-unit box was 16 units
	/// tall, too short for the font it draws (the live reading in batch `20261001-k`).</summary>
	private const float VerticalInset = ControlInnerPadding * 0.5f;

	/// <summary>The width a slider's formatted value keeps at the end of its row.</summary>
	internal const float SliderValueWidth = 56f;

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
	/// Lays a row's own label and control out inside the rect the window's layout gives the row: one
	/// horizontal group on the row, the label flexible, the control at the width its model declared, and a
	/// slider's formatted value after it. The group REPLACES whatever placement the prefab authored, which is
	/// the point — the prefab's own screen is not this window.
	/// </summary>
	internal static void LayOutControlRow(
		GameObject row,
		RectTransform? label,
		RectTransform? control,
		float controlFloor,
		float height,
		RectTransform? valueText)
	{
		var group = row.GetComponent<HorizontalLayoutGroup>();
		if (group is null)
		{
			// A row may carry a group of its own — one that belongs to the game's own screen. CUO's row is one
			// label and one control side by side, so any other group on it goes rather than fighting this one.
			foreach (var other in row.GetComponents<LayoutGroup>())
			{
				Object.Destroy(other);
			}

			group = row.AddComponent<HorizontalLayoutGroup>();
		}

		group.spacing = RowGap;
		group.padding = new RectOffset((int)RowPadding, (int)RowPadding, 0, 0);
		group.childAlignment = TextAnchor.MiddleLeft;
		group.childControlWidth = true;
		group.childControlHeight = true;
		group.childForceExpandWidth = false;
		group.childForceExpandHeight = false;

		if (label != null)
		{
			LayOut(label, flexibleWidth: 1f, width: -1f, height: height);
		}

		if (control != null)
		{
			// The control's width is DECLARED rather than measured: the model's floor and what the game's own
			// prefab is authored with (OnlineUiControlSizing), written straight into the element the group reads
			// — never asked of the control's own content, which reports the box it was just given.
			var width = Mathf.Max(controlFloor, DeclaredWidthOf(control));
			LayOut(control, flexibleWidth: 0f, width: width, height: height);
			LayOutInterior(control, width);
		}

		if (valueText != null)
		{
			LayOut(valueText, flexibleWidth: 0f, width: SliderValueWidth, height: height);
		}
	}

	/// <summary>
	/// What the game's own prefab declares for one control of a row: a control that IS a button (a tab, the
	/// launcher, a page button, a colour block) is as wide as its own caption's text plus the room the row keeps
	/// around it, and every other kind answers from the prefab's own authored size. A field's VALUE is never
	/// measured — it is the player's text, and the model's floor is what says how wide the box is.
	/// </summary>
	private static float DeclaredWidthOf(RectTransform control) =>
		control.GetComponent<Button>() is not null
			? CaptionWidthOf(control)
			: OnlineUiControlSizing.PrefabWidth(control);

	/// <summary>The width the control's own caption needs, or 0 when it carries none (a colour block's caption is
	/// empty, so its prefab width is what stands).</summary>
	private static float CaptionWidthOf(RectTransform control)
	{
		for (var index = 0; index < control.childCount; index++)
		{
			if (control.GetChild(index).GetComponent<TMP_Text>() is { } caption)
			{
				return OnlineUiControlSizing.CaptionWidth(caption);
			}
		}

		return 0f;
	}

	/// <summary>
	/// Prepares the parts of a control the layout above cannot express: a dropdown's value stays on one line (a
	/// wrapped value would double the box and push its arrow out of it) and a field's text accepts the pointer,
	/// because uGUI hit-tests graphics and a field whose text refuses the raycast is one the player cannot
	/// focus. Their RECTS belong to <see cref="LayOutInterior"/>, which runs every frame beside the control's
	/// width.
	/// </summary>
	internal static void PrepareInternals(TMP_Dropdown? dropdown, TMP_InputField? input, Toggle? toggle)
	{
		if (dropdown != null && dropdown.captionText is { } caption)
		{
			// One line, left in the prefab's own alignment: a wrapped value would double the box's height and
			// push the arrow out of it.
			caption.enableWordWrapping = false;
		}

		if (input?.textComponent is { } text)
		{
			text.raycastTarget = true;
		}

		// A checkbox keeps its box where the game's own row put it, sized for that row; a control of this page
		// is one compact height, so the box is centred in it rather than left at the prefab's offset. A slider
		// needs none of this: uGUI's own Slider positions its fill and its handle from the slider's rect, which
		// is why forcing the height is enough for it.
		if (toggle?.targetGraphic is { } box
			&& box.rectTransform != toggle.transform
			&& box.rectTransform.parent == toggle.transform)
		{
			var rect = box.rectTransform;
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.anchoredPosition = Vector2.zero;
		}
	}

	/// <summary>
	/// Places a control's own children on the box it was given, one rect at a time. Everything the game's row
	/// placed by hand is placed again from the box's own numbers: a container (a field's text area, its
	/// placeholder, its caret) fills the box less the room the game's row leaves at its edges, a caption keeps
	/// the width its own text needs and starts at the left edge (so it never runs under the row's art), and a
	/// piece of art (a dropdown's arrow) keeps its own size at the right edge.
	///
	/// <para>
	/// Every rect is computed from the control's own width or from the child's own content, never from a rect a
	/// layout group just wrote, so the pass is idempotent and cannot feed itself. A layout group or a fitter
	/// still sitting on one of these children would fight these writes, so those go.
	/// </para>
	/// </summary>
	private static void LayOutInterior(RectTransform control, float controlWidth)
	{
		var interior = OnlineUiControlBox.InteriorWidth(controlWidth, ControlInnerPadding);
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

			if (!container && authored > 0f && authored < LargestAccessoryWidth)
			{
				// A leaf narrower than a control's own height is the game's own art (a dropdown's arrow): it
				// keeps the size the prefab authored for it at the right-hand edge instead of being stretched
				// with the viewport, which is what the game's own row does with it.
				PinInside(child, ControlInnerPadding, authored, fromTheRight: true);
				continue;
			}

			StretchInside(child, ControlInnerPadding, ControlInnerPadding);
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
	/// the player reads.
	/// </summary>
	private static void HidePrefabBoxArt(RectTransform child)
	{
		if (child.GetComponent<TMP_Text>() != null)
		{
			return;
		}

		if (child.GetComponent<Image>() is { } image)
		{
			image.color = new Color(0f, 0f, 0f, 0f);
		}

		if (child.GetComponent<RawImage>() is { } raw)
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
		text.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, ControlInnerPadding, Mathf.Min(width, interior));
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

	/// <summary>
	/// Makes a control the player can actually hit: the control's own object must carry a graphic that
	/// accepts the raycast, because uGUI hit-tests graphics — a prefab whose field image refuses the pointer
	/// (or which carries no graphic at all) is a box that shows text and answers nothing. Returns true when
	/// this had to change something, which the window reports once per kind.
	/// </summary>
	internal static bool EnsurePointerSurface(GameObject control)
	{
		var graphic = control.GetComponent<Graphic>();
		if (graphic is null)
		{
			var image = control.AddComponent<Image>();
			image.color = new Color(0f, 0f, 0f, 0f);
			graphic = image;
		}

		if (graphic.raycastTarget)
		{
			return false;
		}

		graphic.raycastTarget = true;
		return true;
	}

	/// <summary>A label that is not a control must not swallow a click meant for the control beside or below
	/// it: the game's own rows carry text on every graphic, and a label stretched over a row would take the
	/// click the row's control was waiting for.</summary>
	internal static void MakeLabelTransparentToPointer(TMP_Text? label)
	{
		if (label != null)
		{
			label.raycastTarget = false;
		}
	}

	private static void LayOut(RectTransform rect, float flexibleWidth, float width, float height)
	{
		var element = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
		element.flexibleWidth = flexibleWidth;
		element.minWidth = width > 0f ? width : 0f;
		element.preferredWidth = width;
		element.minHeight = height;
		element.preferredHeight = height;
	}
}
