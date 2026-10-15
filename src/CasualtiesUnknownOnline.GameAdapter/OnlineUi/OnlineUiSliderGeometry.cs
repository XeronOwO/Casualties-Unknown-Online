using CasualtiesUnknownOnline.Runtime.OnlineUi;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// The inside of one slider of a page (ticket online-ui-paired-controls-and-page-scroll, S1): the game's own
/// track, the fraction of it the value fills and the handle the player drags, all of it placed on the compact
/// box a page gives a control and tinted from the page's own palette.
///
/// <para>
/// It exists because the row's own geometry treats every child of a control as a box CUO collapses — the
/// game's decoration is hidden so that one control shows exactly one box, which is right for a field's white
/// frame and a dropdown's border and WRONG here: a slider's children ARE its reading. The user's report of
/// 2026-10-14 is the result of hiding them ("the slider's middle has no thin line, so you cannot see where
/// its ends are"), so a slider's children are placed instead of hidden and keep their own drawing.
/// </para>
///
/// <para>
/// The parts come from the Slider component itself (<c>fillRect</c>, <c>handleRect</c>,
/// <c>targetGraphic</c>) rather than from the names the game's hierarchy happens to use, so a prefab that
/// renames a child still works; a slider whose parts the game left unassigned simply shows its track. The
/// track's thickness and the value field's width live in <see cref="OnlineUiWindowLayout"/> with the rest of
/// the surface's measurements, and everything here is idempotent — it runs once per frame per control.
/// </para>
/// </summary>
internal static class OnlineUiSliderGeometry
{
	/// <summary>The track: the bar the whole range is drawn on, at the panel's own value fill so the control
	/// still reads as one box of the same family as the fields beside it. The track's THICKNESS is whatever
	/// the game's own prefab authored for it — the game draws a line across a row three times this one's
	/// height, and the ratio is what keeps it a line here (measured: 10 units of a 80-unit row, i.e. an
	/// eighth).</summary>
	private static readonly Color TrackColor = new(
		OnlineUiWindowLayout.ControlFillR,
		OnlineUiWindowLayout.ControlFillG,
		OnlineUiWindowLayout.ControlFillB,
		1f);

	/// <summary>The fraction the value fills: the tab row's own warm tone, so the current value is visible at
	/// a glance and not only as a number.</summary>
	private static readonly Color FillColor = new(0.85f, 0.72f, 0.38f, 1f);

	/// <summary>The handle: the light tone the page's captions use, so the thing the player drags reads as
	/// theirs to drag.</summary>
	private static readonly Color HandleColor = new(0.92f, 0.93f, 0.94f, 1f);

	/// <summary>The room the track keeps from the control's own edge. The handle slides inside it, which is
	/// what stops the handle from being half outside the box at either end of the range.</summary>
	private const float TrackInset = OnlineUiBoxGeometry.InnerPadding;

	/// <summary>
	/// Places one slider's own parts on the box its row gave it. The control keeps the ONE box CUO draws
	/// (<see cref="OnlineUiRowGeometry"/> applies it) and the game's track, fill and handle are placed inside
	/// that box from the slider's own component, never by measuring what a layout group just wrote.
	///
	/// <para>
	/// <paramref name="prefabValue"/> is the formatted reading the game's own row prefab carries beside its
	/// slider — the defunct label the user's report replaced with an editable field. It is HIDDEN rather than
	/// placed: leaving it visible would put the same value on screen twice, once of them stale, and its own
	/// rect overlaps the track because the prefab authored it for a screen three times this height.
	/// </para>
	/// </summary>
	internal static void LayOut(Slider slider, RectTransform control, float controlWidth, RectTransform? prefabValue)
	{
		// A page's row is one compact height and the engine places these from the rect the row gave them, so
		// the sizes are written straight onto each part; the pivots are centred because the engine's own `Set`
		// positions a handle by its pivot, and a prefab authored with a corner pivot would drag the handle out
		// of the box as soon as the value moved.
		slider.direction = Slider.Direction.LeftToRight;
		slider.useGUILayout = false;

		PlaceTrack(slider.targetGraphic);
		PlaceContainer(slider.fillRect, control, controlWidth, insetsTheTrack: true);

		// The handle's own CONTAINER is what CUO places, never the handle: a slider drives its handle by the
		// handle's ANCHORS inside the container the game's own prefab put it in, and both that container and
		// those anchors belong to the engine. Stretching the handle's own rect onto the control (what this did
		// before) throws the anchors away: the knob then spans the whole control and reads as its track, and
		// once its size is corrected it parks in the middle of the line whatever the value is (batch
		// `20261015-d` read both: a 571.99-wide handle inside a 571.99-wide slider, then a 36-unit knob centred
		// at the slider's own midpoint with the value at a quarter of it).
		PlaceContainer(HandleContainerOf(slider), control, controlWidth, insetsTheTrack: false);
		PlaceHandle(slider.handleRect);
		Tint(slider);

		if (prefabValue != null)
		{
			prefabValue.gameObject.SetActive(false);
		}
	}

	/// <summary>
	/// The rect the engine moves the handle inside — the game's own container for it — or null when the handle
	/// is a child of the control itself: such a prefab has no container, and the handle's place is then the
	/// control's own rect.
	/// </summary>
	private static RectTransform? HandleContainerOf(Slider slider) =>
		slider.handleRect != null && slider.handleRect.parent is RectTransform parent && parent != slider.transform
			? parent
			: null;

	/// <summary>
	/// The bar itself: the width of the control less the room the handle slides in, at the height the game
	/// authored for it, centred in the box. A target graphic that is not an image (a slider drawing its
	/// track with something else) keeps whatever it is — this only writes the rect.
	/// </summary>
	private static void PlaceTrack(Graphic? track)
	{
		if (track is null)
		{
			return;
		}

		var rect = track.rectTransform;
		var authored = rect.rect.height;
		var height = authored > 0f && authored < OnlineUiWindowLayout.ControlHeight
			? authored
			: OnlineUiWindowLayout.ControlHeight * 0.25f;
		rect.anchorMin = new Vector2(0f, 0.5f);
		rect.anchorMax = new Vector2(1f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.offsetMin = new Vector2(TrackInset, -height * 0.5f);
		rect.offsetMax = new Vector2(-TrackInset, height * 0.5f);
	}

	/// <summary>
	/// One of the slider's two containers — the area its fill is drawn in, and the area its handle slides
	/// along. Both run the width of the track and the full height of the box, so the fill and the handle
	/// stand on the same line; <paramref name="insetsTheTrack"/> is the fill's own inset, because the
	/// engine draws the filled fraction between the handle's two end positions rather than under the room
	/// the handle keeps.
	/// </summary>
	private static void PlaceContainer(RectTransform? container, RectTransform control, float controlWidth, bool insetsTheTrack)
	{
		if (container is null)
		{
			return;
		}

		DropLayoutDrivers(container);
		container.SetParent(control, worldPositionStays: false);
		container.anchorMin = new Vector2(0f, 0f);
		container.anchorMax = new Vector2(1f, 1f);
		container.pivot = new Vector2(0.5f, 0.5f);
		var inset = insetsTheTrack ? Mathf.Min(TrackInset, Mathf.Max(0f, controlWidth * 0.25f)) : 0f;
		container.offsetMin = new Vector2(inset, 0f);
		container.offsetMax = new Vector2(-inset, 0f);
	}

	/// <summary>
	/// The knob itself: a square the height of the compact box, standing where the ENGINE put it. Its place is
	/// the anchors a slider drives as the value moves, so CUO writes the SIZE and the pivot only — writing the
	/// rect's position or its anchors here is what parked the knob in the middle of the line. The width is
	/// written as <c>sizeDelta</c> rather than with <c>SetSizeWithCurrentAnchors</c>, which re-places a
	/// point-anchored rect as a side effect and is the other half of that defect.
	/// </summary>
	private static void PlaceHandle(RectTransform? handle)
	{
		if (handle is null)
		{
			return;
		}

		// Every write here is behind a comparison: this runs once per frame per slider, and a rect write marks
		// the canvas for a rebuild whether or not anything changed.
		if (handle.pivot != new Vector2(0.5f, 0.5f))
		{
			handle.pivot = new Vector2(0.5f, 0.5f);
		}

		if (!handle.sizeDelta.x.Equals(OnlineUiWindowLayout.ControlHeight))
		{
			handle.sizeDelta = new Vector2(OnlineUiWindowLayout.ControlHeight, handle.sizeDelta.y);
		}

		if (!handle.anchoredPosition.y.Equals(0f))
		{
			handle.anchoredPosition = new Vector2(handle.anchoredPosition.x, 0f);
		}
	}

	/// <summary>
	/// Gives the track, the filled fraction and the handle the page's own colours. Their drawing is NOT
	/// hidden (that is the defect this type exists to undo) and their raycast is left alone: the slider
	/// itself is what the player drags, and the parts are its own drawing.
	/// </summary>
	private static void Tint(Slider slider)
	{
		if (slider.targetGraphic is { } track)
		{
			track.color = TrackColor;
		}

		if (slider.fillRect != null && slider.fillRect.GetComponent<Graphic>() is { } fill)
		{
			fill.color = FillColor;
		}

		if (slider.handleRect != null && slider.handleRect.GetComponent<Graphic>() is { } handle)
		{
			handle.color = HandleColor;
		}
	}

	/// <summary>
	/// Takes the layout drivers off a slider's own container: a layout group asked to size it measures the
	/// rect the engine has just written from the value, which is the feedback the row's geometry removes
	/// everywhere else.
	/// </summary>
	private static void DropLayoutDrivers(RectTransform part)
	{
		foreach (var group in part.GetComponents<LayoutGroup>())
		{
			Object.Destroy(group);
		}

		foreach (var fitter in part.GetComponents<ContentSizeFitter>())
		{
			Object.Destroy(fitter);
		}
	}
}
