using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// The geometry INSIDE one of the game's row prefabs (ticket online-ui-layout-and-input-detail-pass, S2).
/// The game's settings screen places its rows' children by hand for its own full-width panel, so a row
/// instantiated into CUO's window keeps those authored rects: a dropdown's box, a field's viewport or a
/// label's text then sits at a position the window never sized, which is what the user's run reported as
/// controls that overlap their neighbour, text clipped mid-word and boxes that cannot be clicked.
///
/// <para>
/// This type is where CUO takes that geometry over. The row's own control is laid out by a layout group the
/// row now carries — its label takes what is left, the control keeps the width measured for it at the right
/// — and a control's own insides (a dropdown's caption, an input field's viewport) are stretched inside the
/// box, so nothing of the game's art is thrown away: the sprite, the 9-slice, the font and the control
/// components are still the game's, only their rectangles are CUO's.
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

	/// <summary>The width a slider's formatted value keeps at the end of its row.</summary>
	internal const float SliderValueWidth = 56f;

	/// <summary>
	/// Lays a row's own label and control out inside the rect the window's layout gives the row: one
	/// horizontal group on the row, the label flexible, the control sized by the ENGINE from its own content
	/// (above the floor the model declared), and a slider's formatted value after it. The group REPLACES
	/// whatever placement the prefab authored, which is the point — the prefab's own screen is not this window.
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
			// The control's width is the ENGINE's answer above the floor the model declared: the control carries
			// a layout group over its own content (a dropdown's caption and arrow, a field's text), so uGUI
			// measures it and CUO writes no width down.
			LayOut(control, flexibleWidth: 0f, width: -1f, height: height);
			control.GetComponent<LayoutElement>().minWidth = controlFloor;
			OnlineUiControlFactory.AddContentGroup(control.gameObject, ControlInnerPadding);
		}

		if (valueText != null)
		{
			LayOut(valueText, flexibleWidth: 0f, width: SliderValueWidth, height: height);
		}
	}

	/// <summary>
	/// Prepares a text control's own insides for the layout that now owns them: the control's content group
	/// places its caption and its arrow (that is where the control's width comes from), so all CUO still says
	/// here is what the group cannot know — a dropdown's value must stay on one line, and an input field's text
	/// must fill the viewport the group sized and accept the pointer, because a field whose text refuses the
	/// raycast is a field the player cannot focus.
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
			Stretch(text.rectTransform, 0f, 0f);
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

	private static void Stretch(RectTransform rect, float left, float right)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.offsetMin = new Vector2(left, 0f);
		rect.offsetMax = new Vector2(-right, 0f);
	}
}
