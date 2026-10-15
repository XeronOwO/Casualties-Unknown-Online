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

	/// <summary>
	/// Places the inside of one control of a row: a slider's own track, fill and handle
	/// (<see cref="OnlineUiSliderGeometry"/>) or the one box every other kind shows
	/// (<see cref="OnlineUiBoxGeometry"/>).
	/// </summary>
	private static void LayOutInterior(RectTransform control, float controlWidth, RectTransform? prefabValue)
	{
		// A slider is the one control of this surface whose own children ARE its reading rather than boxes
		// CUO collapses: the game draws its track, the fraction it fills and its handle there, and the rule
		// that hides a prefab's decoration would delete exactly what the user asked to see (ticket
		// online-ui-paired-controls-and-page-scroll, S1).
		if (control.GetComponent<Slider>() is { } slider)
		{
			OnlineUiSliderGeometry.LayOut(slider, control, controlWidth, prefabValue);
			return;
		}

		OnlineUiBoxGeometry.LayOutInterior(control, controlWidth);
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
		// is one compact height, so the box is centred in it rather than left at the prefab's offset
		// (<see cref="OnlineUiBoxGeometry"/> centres everything else that lives inside a control's box).
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
	/// Lays one element of a page out on the line the window wrapped it into. An element that IS one of the
	/// game's own rows — a dropdown, a field, a slider, a toggle — has its label and its control placed inside
	/// it by <see cref="LayOutControlRow"/>. An element that is its own control — a tab, a page button, a colour
	/// block — has no inside to place and takes the width its model declared on ITSELF, because nothing else
	/// writes it: the content group such an element carries measures a CAPTION, so a colour block, which has
	/// none, was drawn as wide as that group's own padding (batch `20261014-a`: 88 declared by the drawer, 18
	/// rendered). A label keeps its own rule — it takes what its line has left — which is why a width of zero
	/// writes nothing.
	///
	/// <para>
	/// The same branch carries the PLACEHOLDER of a kind whose game prefab could not be loaded: such an element
	/// has no control child either, so it takes the width the model declared rather than the width its own text
	/// measured, which is what a page shows when the game moves a prefab.
	/// </para>
	///
	/// <para>
	/// <paramref name="columnWidth"/> is the page's own control column (ticket
	/// online-ui-paired-controls-and-page-scroll, S3): every element of a row that carries a label and a
	/// control gets that label the same width, so the controls of a page line up in one column however long
	/// their names are. Zero means the page did not ask for one — the window measures one over the whole page
	/// it is applying — and each row then keeps the flexible label it always had.
	/// </para>
	/// </summary>
	internal static void LayOutRow(
		GameObject root,
		LayoutElement layout,
		RectTransform? label,
		RectTransform? control,
		float controlWidth,
		float columnWidth,
		RectTransform? prefabValue = null)
	{
		if (control is null)
		{
			if (controlWidth > 0f)
			{
				layout.minWidth = controlWidth;
				layout.preferredWidth = controlWidth;
			}

			return;
		}

		LayOutControlRow(root, label, control, controlWidth, OnlineUiWindowLayout.ControlHeight, columnWidth, prefabValue);
	}

	/// <summary>
	/// Lays a paired control out (ticket online-ui-paired-controls-and-page-scroll, S3): the name on the
	/// line the row opens on, and the control it names — a slider — on the line below it, where the row's
	/// own name takes none of the width the control needs. It is the same group as
	/// <see cref="LayOutControlRow"/> with the two children stacked; a row whose prefab carries no control
	/// is left to that method, which is what a placeholder for a moved prefab is.
	/// </summary>
	internal static void LayOutPairedRow(GameObject root, LayoutElement layout, RectTransform? label, RectTransform? control, float controlWidth, RectTransform? prefabValue = null)
	{
		if (control is null)
		{
			LayOutRow(root, layout, label, control, controlWidth, columnWidth: 0f, prefabValue);
			return;
		}

		var row = (RectTransform)root.transform;
		ConfigureGroup<VerticalLayoutGroup>(
			row.GetComponent<VerticalLayoutGroup>(),
			root,
			TextAnchor.UpperLeft,
			forceExpandHeight: false);

		// A paired row's own prefab frame goes for the same reason a side-by-side row's does: the page shows
		// the box CUO draws around the control, not the game's decoration around the whole line.
		OnlineUiBoxGeometry.HidePrefabBoxArt(row);

		if (label != null)
		{
			// The name on the first line is a LABEL in the full sense: it wraps rather than being cut, and TMP
			// (the layout element that knows its own wrapped text) decides the height it takes.
			LayOut(label, flexibleWidth: 1f, width: -1f, height: -1f);
		}

		if (control != null)
		{
			var width = Mathf.Max(controlWidth, DeclaredWidthOf(control));
			LayOut(control, flexibleWidth: 0f, width: width, height: OnlineUiWindowLayout.ControlHeight);
			LayOutInterior(control, width, prefabValue);
		}
	}

	/// <summary>
	/// Lays a row's own label and control out inside the rect the window's layout gives the row: one
	/// horizontal group on the row, the label flexible, the control at the width its model declared. The
	/// group REPLACES whatever placement the prefab authored, which is the point — the prefab's own screen
	/// is not this window.
	///
	/// <para>
	/// A slider's formatted value is NOT a third child of this group any more: it is a field the player
	/// types into, laid out by <see cref="OnlineUiSliderGeometry"/> beside the track it mirrors (ticket
	/// online-ui-paired-controls-and-page-scroll, S2).
	/// </para>
	/// </summary>
	private static void LayOutControlRow(
		GameObject row,
		RectTransform? label,
		RectTransform? control,
		float controlFloor,
		float height,
		float columnWidth,
		RectTransform? prefabValue)
	{
		ConfigureGroup<HorizontalLayoutGroup>(
			row.GetComponent<HorizontalLayoutGroup>(),
			row,
			TextAnchor.MiddleLeft,
			forceExpandHeight: false);

		// The row's OWN art is the game's screen decoration: a grey frame it draws around every row of its own
		// full-width settings screen, which reads on a page as a box around every row (the user's report of
		// 2026-10-15: "the light grey box in the background is still there"). An element that is a row of a page
		// is a CONTAINER CUO arranges, not a control, so its drawing goes and the page's own fill shows through;
		// its rect and its raycast stay, because a click inside the window must still land on CUO's surface. The
		// element that IS its own control never reaches this method, so a button, a tab, a colour block and the
		// launcher keep the game's art.
		OnlineUiBoxGeometry.HidePrefabBoxArt((RectTransform)row.transform);

		if (label != null)
		{
			// A row's name keeps the page's own control column when the page asked for one, so the controls
			// below it line up; without one it is flexible, which is what the side-by-side rows always were.
			LayOut(
				label,
				flexibleWidth: columnWidth > 0f ? 0f : 1f,
				width: columnWidth > 0f ? columnWidth : -1f,
				height: height);
		}

		if (control != null)
		{
			// The control's width is DECLARED rather than measured: the model's floor and what the game's own
			// prefab is authored with (OnlineUiControlSizing), written straight into the element the group reads
			// — never asked of the control's own content, which reports the box it was just given.
			var width = Mathf.Max(controlFloor, DeclaredWidthOf(control));
			LayOut(control, flexibleWidth: 0f, width: width, height: height);
			LayOutInterior(control, width, prefabValue);
		}
	}

	/// <summary>
	/// The group one of a page's rows carries: a horizontal group for the side-by-side shape and a vertical
	/// one for the paired shape, both with the same spacing and the same page's own room at the edges. Any
	/// group the game's own prefab brought for its own screen goes first — CUO's row is not that screen —
	/// and a group of the right kind already in place is reused rather than rebuilt, because this runs once
	/// per frame per element.
	/// </summary>
	private static T ConfigureGroup<T>(LayoutGroup? group, GameObject row, TextAnchor alignment, bool forceExpandHeight)
		where T : HorizontalOrVerticalLayoutGroup
	{
		if (group is T existing)
		{
			existing.spacing = RowGap;
			existing.padding = new RectOffset((int)RowPadding, (int)RowPadding, 0, 0);
			existing.childAlignment = alignment;
			existing.childControlWidth = true;
			existing.childControlHeight = true;
			existing.childForceExpandWidth = false;
			existing.childForceExpandHeight = forceExpandHeight;
			return existing;
		}

		// A row may carry a group of its own — one that belongs to the game's own screen — so any other group
		// on it goes rather than fighting this one.
		foreach (var other in row.GetComponents<LayoutGroup>())
		{
			Object.Destroy(other);
		}

		var added = row.AddComponent<T>();
		added.spacing = RowGap;
		added.padding = new RectOffset((int)RowPadding, (int)RowPadding, 0, 0);
		added.childAlignment = alignment;
		added.childControlWidth = true;
		added.childControlHeight = true;
		added.childForceExpandWidth = false;
		added.childForceExpandHeight = forceExpandHeight;
		return added;
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

	/// <summary>
	/// Declares one part's place in its row's group: the width it asks for and the height it takes, plus the
	/// anchors that make a group place it where it belongs. A WIDTH of zero or less means "no floor" — the
	/// part is flexible and takes what its line has left — and a height below zero means "your own", which is
	/// what a label whose text wraps asks for (TMP is the layout element that knows that height).
	///
	/// <para>
	/// The vertical anchors are written here rather than left where the prefab put them: a label a group is
	/// told to size must be anchored along the top or centre of a row whose height it does not decide, and
	/// the game's own rows (authored for a screen three times this one's height) anchor theirs at a point
	/// that lands outside the compact box.
	/// </para>
	/// </summary>
	private static void LayOut(RectTransform rect, float flexibleWidth, float width, float height)
	{
		var anchored = rect.anchorMin.y == rect.anchorMax.y;
		if (anchored)
		{
			var centre = height > 0f ? 0.5f : 1f;
			rect.anchorMin = new Vector2(rect.anchorMin.x, centre);
			rect.anchorMax = new Vector2(rect.anchorMax.x, centre);
			rect.pivot = new Vector2(rect.pivot.x, height > 0f ? 0.5f : 1f);
			rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 0f);
		}

		var element = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
		element.flexibleWidth = flexibleWidth;
		element.minWidth = width > 0f ? width : 0f;
		element.preferredWidth = width;
		if (height >= 0f)
		{
			element.minHeight = height;
			element.preferredHeight = height;
		}
		else
		{
			// A height below zero is "as tall as your own content": the one-line floor belongs to the caller
			// (OnlineUiControlView knows the game's own font size), and the preferred height is the text's.
			element.preferredHeight = -1f;
		}
	}
}
