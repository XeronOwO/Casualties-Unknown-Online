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
///
/// <para>
/// A row has two SHAPES and both end in the same body: <see cref="LayOutRow"/> for the row that shares its
/// line, and <see cref="LayOutPairedRow"/> for the one the model sent its control to a line of its own — the
/// row then IS that line, and the elements beside the control are laid out inside it.
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

		LayOutControlRow(root, label, control, controlWidth, OnlineUiWindowLayout.ControlHeight, columnWidth, prefabValue, ownsItsLine: false);
	}

	/// <summary>
	/// Lays a PAIRED control out (ticket online-ui-paired-controls-and-page-scroll, S3): the model sent this
	/// control to a line of its own (<see cref="OnlineUiElementModel.NewLine"/>), so its row IS that line — the
	/// row's box takes the line's whole width and the elements the model put BESIDE the control are laid out
	/// inside it (the surface asks <see cref="OnlineUiControlView.OwnsItsLine"/> and parents them here). The
	/// name is an element of its own on the line above, which is what
	/// <see cref="OnlineUiElementModel.Paired"/> writes.
	///
	/// <para>
	/// What differs from <see cref="LayOutRow"/> is where the row's room goes. A row that owns its line keeps
	/// it at the LEADING edge only, because the element that closes the line is a row of the game's own — the
	/// value field a numeric rule is bound to — and the room inside THAT is the page's margin there: the same
	/// six units the control on its left keeps from the page's other edge. Keeping the room at both ends is
	/// what the acceptance reading of batch `20261015-c` caught the hard way: the row's box was 132 units wide
	/// inside a 984-unit page, because the row declared no width of its own and the engine answered with the
	/// width of the control inside it — and the field beside it, a row of its own 390 units wide, was drawn
	/// from 268 units PAST that box, which is what the user saw as two widgets floating with no margin.
	/// </para>
	///
	/// <para>
	/// The control's own parts are placed at the width the model declared — the least width it can have —
	/// because the box the line gives it is not known until the engine has laid the line out, and a slider's
	/// parts are anchored to the box rather than measured from it: the one number that reads that width (the
	/// room its track keeps clear) answers the same at the floor and above it.
	/// </para>
	/// </summary>
	internal static void LayOutPairedRow(GameObject root, LayoutElement layout, RectTransform? label, RectTransform? control, float controlWidth, float columnWidth, RectTransform? prefabValue = null)
	{
		if (control is null)
		{
			// A row whose prefab carries no control cannot hold the elements beside it — it is the placeholder
			// a moved prefab leaves — so it takes the side-by-side shape instead.
			LayOutRow(root, layout, label, control, controlWidth, columnWidth, prefabValue);
			return;
		}

		LayOutControlRow(root, label, control, controlWidth, OnlineUiWindowLayout.ControlHeight, columnWidth, prefabValue, ownsItsLine: true);
	}

	/// <summary>
	/// Lays a row's own label and control out inside the rect the window's layout gives the row: one
	/// horizontal group on the row, the label in the page's control column (or flexible without one), and the
	/// control at the width its model declared. The group REPLACES whatever placement the prefab authored,
	/// which is the point — the prefab's own screen is not this window.
	///
	/// <para>
	/// <paramref name="ownsItsLine"/> is the shape the paired control takes: the row holds the whole line its
	/// control was sent to, so its control takes what that line has left (the elements beside it are what
	/// closes the line) and the row keeps the page's room at its leading edge only. The row's own
	/// <see cref="LayoutElement"/> is made flexible beside this, by the view that owns it
	/// (<see cref="OnlineUiControlView.ApplyLayout"/>): the two together are what make the row the page's
	/// width rather than the width of its own content.
	/// </para>
	///
	/// <para>
	/// A slider's formatted value is NOT a third child of this group any more: it is a field the player types
	/// into, laid out by <see cref="OnlineUiSliderGeometry"/> beside the track it mirrors (ticket
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
		RectTransform? prefabValue,
		bool ownsItsLine)
	{
		ConfigureGroup(
			row.GetComponent<HorizontalLayoutGroup>(),
			row,
			trailingRoom: ownsItsLine ? 0 : (int)RowPadding);

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
			// — never asked of the control's own content, which reports the box it was just given. On a line the
			// row OWNS it is that width and then what the line has left, so the value beside the control is what
			// closes the row.
			var width = Mathf.Max(controlFloor, DeclaredWidthOf(control));
			LayOut(control, flexibleWidth: ownsItsLine ? 1f : 0f, width: width, height: height);
			LayOutInterior(control, width, prefabValue);
		}
	}

	/// <summary>
	/// The group one of a page's rows carries: one horizontal group, the page's own room at the leading edge,
	/// the page's gap between the parts. Any group the game's own prefab brought for its own screen goes first
	/// — CUO's row is not that screen — and a group already in place is reused rather than rebuilt, because
	/// this runs once per frame per element.
	///
	/// <para>
	/// Every write here is behind a comparison, because uGUI marks a layout dirty whenever one of these
	/// properties CHANGES — and a <see cref="RectOffset"/> compares by reference, so writing a fresh one every
	/// frame would have the engine re-lay the whole page out every frame (the independent review of this
	/// cycle proved it from the engine's own IL).
	/// </para>
	///
	/// <para>
	/// <paramref name="trailingRoom"/> is zero for a row that owns its line: what closes that line is an
	/// element with room of its own inside it, and the page's margin there is that element's — see
	/// <see cref="LayOutPairedRow"/>.
	/// </para>
	/// </summary>
	private static HorizontalLayoutGroup ConfigureGroup(HorizontalLayoutGroup? group, GameObject row, int trailingRoom)
	{
		if (group is { } existing)
		{
			SetGroup(existing, trailingRoom);
			return existing;
		}

		// A row may carry a group of its own — one that belongs to the game's own screen — so any other group
		// on it goes rather than fighting this one.
		foreach (var other in row.GetComponents<LayoutGroup>())
		{
			Object.Destroy(other);
		}

		var added = row.AddComponent<HorizontalLayoutGroup>();
		SetGroup(added, trailingRoom);
		return added;
	}

	/// <summary>Writes a row's own group: one line of parts, the label beside the control, nothing stretched
	/// across the row's height.</summary>
	private static void SetGroup(HorizontalLayoutGroup group, int trailingRoom)
	{
		if (!group.spacing.Equals(RowGap))
		{
			group.spacing = RowGap;
		}

		if (group.padding.left != (int)RowPadding || group.padding.right != trailingRoom)
		{
			group.padding = new RectOffset((int)RowPadding, trailingRoom, 0, 0);
		}

		if (group.childAlignment != TextAnchor.MiddleLeft)
		{
			group.childAlignment = TextAnchor.MiddleLeft;
		}

		if (!group.childControlWidth)
		{
			group.childControlWidth = true;
		}

		if (!group.childControlHeight)
		{
			group.childControlHeight = true;
		}

		if (group.childForceExpandWidth)
		{
			group.childForceExpandWidth = false;
		}

		if (group.childForceExpandHeight)
		{
			group.childForceExpandHeight = false;
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
