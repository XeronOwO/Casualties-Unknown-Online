using CasualtiesUnknownOnline.Runtime.OnlineUi;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// The inside of one slider of a page (ticket online-ui-paired-controls-and-page-scroll, S1): the game's own
/// track, the fraction of it the value fills and the handle the player drags, and the rule that the game's own
/// interior is left where the game put it.
///
/// <para>
/// It exists because the row's own geometry treats every child of a control as a box CUO collapses — the
/// game's decoration is hidden so that one control shows exactly one box, which is right for a field's white
/// frame and a dropdown's border and WRONG here: a slider's children ARE its reading. The user's report of
/// 2026-10-14 is the result of hiding them ("the slider's middle has no thin line, so you cannot see where
/// its ends are"), so a slider's children are kept and tinted instead of hidden.
/// </para>
///
/// <para>
/// What this type does NOT do is re-cut them. The first cut re-placed the rail, the fill and the handle onto
/// the compact box the row gives a control, and the acceptance reading of batch `20261015-d` is what that
/// produced: a fill <c>551.99x36.00</c> stretched over the whole control — the frame draws it 36 units tall —
/// inside a rail that resolves to <c>571.99x28.00</c> in the same row, which is the user's "the yellow
/// rectangle in front and the background's full rectangle do not overlap exactly" (2026-10-15). The same
/// dump is what shows WHY: this prefab's <c>Slider.targetGraphic</c> carries <c>whitesquare</c> with the
/// handle's own <c>0.92,0.93,0.94</c>, i.e. it IS the handle — the rail is the sibling named
/// <c>Background</c> — so the pass that "placed the track" was placing and tinting the knob, while the engine
/// drove the same rect from the value.
/// </para>
///
/// <para>
/// So the game's own boxes stand, and the engine drives them: the filled fraction inside the prefab's own
/// <c>Fill Area</c>, the knob inside the box the engine slides it in. The ONE thing re-cut here is that box,
/// because it decides TWO numbers at once — where the knob's centre travels, and how tall the knob is (the
/// engine stretches the handle across its container and the prefab adds its own nine units on top: the frame
/// of batch `20261015-d` renders a 36-unit container as a 45-unit knob). A travel box this surface writes is
/// therefore also the number that says how tall the knob reads, and the compact row is what that box has to
/// be; the box the game's own row would resolve to is unread — the prefab ASSET still carries it, which is
/// what <c>tools/acceptance/recipes/cuo-ui-prefab.cs</c> reads and the acceptance run judges.
/// </para>
///
/// <para>
/// What the fraction's own box lands on is the same kind of reading: it is the game's <c>Fill Area</c> plus
/// whatever size the prefab adds to it, and only the asset can say what that is. Everything here is
/// idempotent — it runs once per frame per control — and every RECT write is behind a comparison, because a
/// rect write marks the canvas for a rebuild whether or not anything changed.
/// </para>
/// </summary>
internal static class OnlineUiSliderGeometry
{
	/// <summary>The knob's own width: the height of one control, so the knob of a slider reads as a square of
	/// the same size as the checkbox beside it in the page's own column.</summary>
	private const float KnobWidth = OnlineUiWindowLayout.ControlHeight;

	/// <summary>The fraction the value fills: the tab row's own warm tone, so the current value is visible at
	/// a glance and not only as a number.</summary>
	private static readonly Color FillColor = new(0.85f, 0.72f, 0.38f, 1f);

	/// <summary>The knob: the light tone the page's captions use, so the thing the player drags reads as
	/// theirs to drag.</summary>
	private static readonly Color HandleColor = new(0.92f, 0.93f, 0.94f, 1f);

	/// <summary>
	/// Prepares one slider of a page: the parts the game drew stay where the game put them and take the page's
	/// own colours, the knob's travel is re-cut for the compact row, and the defunct formatted value the
	/// prefab carries beside its slider is hidden.
	///
	/// <para>
	/// <paramref name="prefabValue"/> is that formatted reading — the label the user's report replaced with an
	/// editable field. It is HIDDEN rather than placed: leaving it visible would put the same value on screen
	/// twice, one of them stale, and its own rect sits exactly where this row's value field is drawn (batch
	/// `20261015-d` reads the label at <c>412.44..485.96</c> in the same row as the field's
	/// <c>366.00..486.00</c>).
	/// </para>
	/// </summary>
	internal static void LayOut(Slider slider, RectTransform? prefabValue)
	{
		// The engine places the rail's fraction and the knob from the value, so CUO writes only what the engine
		// cannot: the direction, the travel the knob is placed in, and the colours of the two parts it draws.
		slider.direction = Slider.Direction.LeftToRight;
		slider.useGUILayout = false;

		Tint(slider);
		PlaceKnobTravel(slider);

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
	/// The one layer of the prefab this surface re-cuts: the box the engine slides the knob in. A slider drives
	/// its handle by the handle's ANCHORS inside that box, so the box decides both where the knob's centre
	/// travels and how tall the knob renders — the prefab's own box is authored against the game's own row, and
	/// the batch `20261015-d` reading of what that produces in this one is a <c>607.99x9.00</c> handle
	/// stretched across the whole control, a knob taller than the row it lives in.
	///
	/// <para>
	/// It takes the control's own box, less half a knob at each end: the knob's centre then travels from half a
	/// knob inside the rail's left end to half a knob inside its right, so a value at either end of the range
	/// leaves the knob ON the rail rather than hanging off the page — which is also why the room follows the
	/// knob's width, a number THIS surface chooses and the prefab cannot have authored for. The knob's HEIGHT
	/// comes from this box rather than from its own rect, because the engine stretches the handle across the
	/// container: the size written here is a width and a zero.
	/// </para>
	///
	/// <para>
	/// A prefab whose handle is a child of the control has box and knob in one object and the engine drives
	/// neither, so there the game's own place stands exactly as the rail's and the fill's do, and nothing but
	/// the knob's pivot is levelled.
	/// </para>
	/// </summary>
	private static void PlaceKnobTravel(Slider slider)
	{
		var handle = slider.handleRect;
		var container = HandleContainerOf(slider);
		if (container is null)
		{
			CentreTheKnob(handle);
			return;
		}

		// A group or a fitter left on the box the knob slides in would fight the anchors the engine writes
		// from the value, which is why the one box CUO writes is also the one it clears of drivers.
		DropLayoutDrivers(container);

		if (container.anchorMin != Vector2.zero)
		{
			container.anchorMin = Vector2.zero;
		}

		if (container.anchorMax != Vector2.one)
		{
			container.anchorMax = Vector2.one;
		}

		if (container.pivot != new Vector2(0.5f, 0.5f))
		{
			container.pivot = new Vector2(0.5f, 0.5f);
		}

		var room = KnobWidth * 0.5f;
		var left = new Vector2(room, 0f);
		var right = new Vector2(-room, 0f);
		if (container.offsetMin != left)
		{
			container.offsetMin = left;
		}

		if (container.offsetMax != right)
		{
			container.offsetMax = right;
		}

		if (handle is not null && handle.sizeDelta != new Vector2(KnobWidth, 0f))
		{
			handle.sizeDelta = new Vector2(KnobWidth, 0f);
		}

		CentreTheKnob(handle);
	}

	/// <summary>
	/// Puts a knob's pivot at its own centre: the engine places a handle by its pivot, and a prefab authored
	/// with a corner pivot would drag the knob out of the box as soon as the value moved. The anchored
	/// position is levelled for the same reason — the engine drives the handle's ANCHORS, never its offset
	/// from them — and every write here is behind a comparison.
	/// </summary>
	private static void CentreTheKnob(RectTransform? handle)
	{
		if (handle is null)
		{
			return;
		}

		if (handle.pivot != new Vector2(0.5f, 0.5f))
		{
			handle.pivot = new Vector2(0.5f, 0.5f);
		}

		if (!handle.anchoredPosition.y.Equals(0f))
		{
			handle.anchoredPosition = new Vector2(handle.anchoredPosition.x, 0f);
		}
	}

	/// <summary>
	/// Gives the filled fraction and the knob the page's own colours. The RAIL keeps the game's own: it is the
	/// one part of the control that draws the range the player chooses from, it is authored as a frame whose
	/// middle is the page's own surface (the frame of batch `20261015-d` draws its grey edges and nothing
	/// between them), and a fill in the dark tone a control of this page carries would draw over the very line
	/// the user asked to see.
	///
	/// <para>
	/// The knob is coloured from <c>handleRect</c> and never from <c>targetGraphic</c>: in this prefab they are
	/// the same object (the dump of batch `20261015-d` reads <c>bgSprite: whitesquare</c> beside the handle's
	/// own sprite), so a "track" colour written there is a colour written to the knob.
	/// </para>
	/// </summary>
	private static void Tint(Slider slider)
	{
		if (slider.fillRect != null && slider.fillRect.GetComponent<Graphic>() is { } fill && fill.color != FillColor)
		{
			fill.color = FillColor;
		}

		if (slider.handleRect != null && slider.handleRect.GetComponent<Graphic>() is { } handle && handle.color != HandleColor)
		{
			handle.color = HandleColor;
		}
	}

	/// <summary>
	/// Takes the layout drivers off the box the knob slides in: a layout group asked to size it measures the
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
