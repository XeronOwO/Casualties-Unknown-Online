using CasualtiesUnknownOnline.Runtime.OnlineUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;
/// <summary>
/// How wide a control of the Online UI window has to be (ticket online-ui-layout-and-input-detail-pass, S2,
/// reworked by its own acceptance pass). Two things decide it: the width the MODEL declared, which is a floor
/// and nothing more, and the intrinsic width of the control's own content — the size the game's own prefab
/// was authored with for a button, a toggle, a slider and a field, and the caption TMP measured for a
/// dropdown. The larger of the two is the control's width, and it is DECLARED to uGUI (a
/// <see cref="LayoutElement.preferredWidth"/>) rather than left for the engine to measure.
///
/// <para>
/// That last part is the rework, and the reason this type no longer asks
/// <see cref="LayoutUtility.GetPreferredSize"/> for the answer. uGUI measures a layout group's child by
/// walking the child's own content, and content that is STRETCHED inside the box (a field's text area, its
/// caret, a dropdown's caption) reports the width the parent's group just wrote: the measurement reads its
/// own output. The batch `20261001-k` probe caught the feedback running away — a dropdown 690,000 units wide
/// inside a 984-unit page, doubling every frame or two — while the value it held stayed correct. Measuring
/// what cannot be measured is what this file used to do; <see cref="OnlineUiControlBox"/> holds the rule that
/// replaced it.
/// </para>
///
/// <para>
/// The floor is still not a ceiling. A long caption, a translated game, a value the player typed into a field
/// that sizes to its content: each of them reports MORE than the model's hint and widens the control, which is
/// what keeps an English label from being cut inside a box sized for Chinese.
/// </para>
/// </summary>
internal static class OnlineUiControlSizing
{
	/// <summary>
	/// The width the control takes in the row it is laid out in: the model's own hint, and above it whatever
	/// the control's own content needs. A label is the exception — it takes what the row has left and counts
	/// as nothing towards the wrap, so it reports 0 here. A field and a dropdown also fall back to the layout's
	/// own minimum when neither the model nor the game's prefab declares a width, because a control the player
	/// types into cannot be zero wide.
	/// </summary>
	internal static float EffectiveWidth(OnlineUiElementModel element, float contentWidth) => element.Kind switch
	{
		OnlineUiElementKind.Label => 0f,
		OnlineUiElementKind.TextField or OnlineUiElementKind.Dropdown =>
			OnlineUiControlBox.EffectiveWidth(WithMinimum(element.Width), contentWidth),
		_ => OnlineUiControlBox.EffectiveWidth(element.Width, contentWidth),
	};

	/// <summary>
	/// The intrinsic width of a control whose prefab is ALL that can be asked, for a kind whose own content is
	/// stretched inside it (a field's text area, a toggle's box): the width the game's own prefab authored for
	/// it. Its STRETCHED content is never measured — that is the feedback the acceptance pass of batch
	/// `20261001-k` caught running away — so a kind whose content is the player's own (a text field) answers 0
	/// here and leaves the model's floor as the whole answer.
	/// </summary>
	internal static float PrefabWidth(RectTransform rect) =>
		rect.GetComponent<TMP_InputField>() != null ? 0f : LayoutUtility.GetPreferredSize(rect, 0);

	/// <summary>
	/// The width one caption's own text needs, plus the room the game's own row keeps around its text: the
	/// caption is the ONE thing whose preferred size does not depend on the box it sits in (TMP measures the
	/// text, not the rect), which is why a button's width is read from it — an English caption in a button sized
	/// for a Chinese one is the user's report of 2026-10-01 (`Preferences` running into its border).
	/// </summary>
	internal static float CaptionWidth(TMP_Text caption)
	{
		var values = caption.GetPreferredValues();
		var text = caption.text;
		var lines = text == null ? null : text.Split('\n');
		if (lines is { Length: > 1 })
		{
			// A caption with a line break is measured one line at a time: the widest line is the width it needs.
			var widest = 0f;
			foreach (var line in lines)
			{
				var single = caption.GetPreferredValues(line);
				if (single.x > widest)
				{
					widest = single.x;
				}
			}

			return widest > 0f ? widest + (2f * caption.fontSize) : 0f;
		}

		return values.x > 0f ? values.x + (2f * caption.fontSize) : 0f;
	}

	/// <summary>
	/// The width a field or a dropdown falls back to when neither the model nor the prefab declares one: a
	/// field is the one control whose content is the player's, so a floor nobody declared is the layout's own
	/// minimum rather than zero.
	/// </summary>
	internal static float WithMinimum(float width) =>
		width > 0f ? width : OnlineUiWindowLayout.MinimumControlWidth;

	/// <summary>Whether the element takes the whole row it is on: a label with no width of its own takes what
	/// is left, and the four kinds built on one of the game's own rows (a label with a control) are a row in
	/// themselves.</summary>
	internal static bool FillsTheRow(OnlineUiElementModel element) => element.Kind switch
	{
		OnlineUiElementKind.Label => element.Width <= 0f,
		OnlineUiElementKind.Toggle or OnlineUiElementKind.Dropdown or OnlineUiElementKind.TextField or OnlineUiElementKind.Slider => true,
		_ => false,
	};
}
