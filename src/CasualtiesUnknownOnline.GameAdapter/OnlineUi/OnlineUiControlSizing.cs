using CasualtiesUnknownOnline.Runtime.OnlineUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;
/// <summary>
/// How wide a control of the Online UI window has to be (ticket online-ui-layout-and-input-detail-pass, S2,
/// reworked by its own acceptance pass and again by the cycle of 2026-10-15). Two things decide it: the width
/// the MODEL declared, which is a floor and nothing more, and the intrinsic width of the element's own content
/// — the caption TMP measured (plus the room the game's own row keeps around its text) for a button and a
/// colour block, the size the game's own prefab authored for the control a row carries (a toggle's checkbox, a
/// slider, a field's box), a stretched control that declares none falling back to the layout's own minimum. The
/// larger of the two is the element's width, and it is DECLARED to uGUI (a
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
/// what keeps an English label from being cut inside a box sized for Chinese. The room a caption keeps is added
/// exactly ONCE (<see cref="OnlineUiControlBox.CaptionedWidth"/>) — a path that measured the caption and added
/// the room on top of it measured six English tabs at 1038.7 units inside a 984-unit page, the user's report of
/// 2026-10-14 — and <see cref="MinimumWidth"/> is the same rule at its other end: the least a control can be
/// drawn at while still showing its caption whole.
/// </para>
/// </summary>
internal static class OnlineUiControlSizing
{
	/// <summary>The room a caption keeps when its control is drawn as narrow as it can be: the room the control's
	/// own box keeps between its edge and its content, rather than the room a row keeps around a control. A tab
	/// of a strip too narrow for every caption gives this up before any of its own text does
	/// (<see cref="OnlineUiTabLayout"/>), so the least a tab is drawn at still shows its whole caption.</summary>
	private const float MinimumCaptionRoom = 2f * OnlineUiWindowLayout.ControlInnerPadding;

	/// <summary>
	/// The width ONE element takes on its line — the rule the window's wrap is asked with and the width the
	/// element is then laid out at, in one place. The model's hint is a FLOOR and above it the answer is the
	/// element's own content: the caption TMP measured (plus the room the game's own row keeps around its text)
	/// for a button and a colour block, the size the game authored for a control that came with one, the
	/// layout's own minimum under the three kinds the player types, picks or drags in (a field, a dropdown, a
	/// slider — a control the player operates cannot be zero wide). A label is the exception: it takes what its
	/// row has left and counts as nothing towards the wrap, so it reports 0 here.
	///
	/// <para>
	/// Nothing here measures a box a layout group has just written. A toggle and a slider are answered from
	/// <paramref name="authoredControlWidth"/> — the width read off the CONTROL when the view was built
	/// (<see cref="AuthoredChildWidth"/>) — because the element that CONTAINS a control reports the row's own
	/// sum, which contains the control's declared width: that is how the Admin page's rule rows grew 7282 →
	/// 30537 units inside a 984-unit page while their flexible label, whose minimum is zero, was squeezed to
	/// one character per line (batch `20261015-a`).
	/// </para>
	/// </summary>
	internal static float EffectiveWidth(
		OnlineUiElementModel element,
		TMP_Text? caption,
		float typographySize,
		float authoredControlWidth) => element.Kind switch
		{
			OnlineUiElementKind.Label => 0f,
			OnlineUiElementKind.Button or OnlineUiElementKind.ColorSwatch =>
				OnlineUiControlBox.CaptionedWidth(element.Width, CaptionTextWidthOrZero(caption), 2f * typographySize),
			OnlineUiElementKind.TextField or OnlineUiElementKind.Dropdown or OnlineUiElementKind.NumberField =>
				OnlineUiControlBox.EffectiveWidth(WithMinimum(element.Width), 0f),
			OnlineUiElementKind.Toggle =>
				OnlineUiControlBox.EffectiveWidth(element.Width, authoredControlWidth),
			OnlineUiElementKind.Slider =>
				OnlineUiControlBox.EffectiveWidth(WithMinimum(element.Width), authoredControlWidth),
			_ => 0f,
		};

	/// <summary>
	/// The least width the element can be given without cutting its caption: the same rule as
	/// <see cref="EffectiveWidth"/> with the room the control's OWN BOX keeps between its edge and its content,
	/// rather than the room a row keeps around a control. It is the floor a tab of a strip that cannot fit every
	/// caption gives up its room down to (<see cref="OnlineUiTabLayout"/>).
	///
	/// <para>
	/// Only a kind whose width IS its caption has room to give up; a toggle is the size the game authored for
	/// its checkbox, a slider and a field are widths the player drags or types into, and none of them answers
	/// here — a zero answer is what tells the strip's rule that this control cannot shrink at all.
	/// </para>
	/// </summary>
	internal static float MinimumWidth(
		OnlineUiElementModel element,
		TMP_Text? caption) => element.Kind switch
		{
			OnlineUiElementKind.Button or OnlineUiElementKind.ColorSwatch =>
				OnlineUiControlBox.CaptionedWidth(element.Width, CaptionTextWidthOrZero(caption), MinimumCaptionRoom),
			_ => 0f,
		};

	/// <summary>The width a caption needs, or 0 for a kind whose caption is not its content (a colour block's
	/// caption is empty, and a field's row label is not the value it holds).</summary>
	private static float CaptionTextWidthOrZero(TMP_Text? caption) => caption != null ? CaptionTextWidth(caption) : 0f;

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
	/// The width the game's own prefab authored for ONE of a row's children — the toggle's checkbox, a
	/// dropdown's or a field's box — read from that child's own rect. It is a reading of the PREFAB, so it
	/// belongs to the moment a view is built: the row's own layout group writes the child's rect from the
	/// first pass on, and asking afterwards reads the box CUO itself just wrote (the feedback the acceptance
	/// pass of batch `20261001-k` caught running away). <see cref="OnlineUiControlView"/> therefore takes this
	/// once, when it creates the view, and keeps it.
	///
	/// <para>
	/// A child the game anchored at a POINT carries its authored size in <c>sizeDelta</c>; one the game
	/// stretched across its own row — the float row's slider — declares no width of its own and reports 0,
	/// which leaves the model's floor as the whole answer.
	/// </para>
	/// </summary>
	internal static float AuthoredChildWidth(RectTransform? child) =>
		child != null && Mathf.Approximately(child.anchorMin.x, child.anchorMax.x) && child.rect.width > 0f
			? child.rect.width
			: 0f;

	/// <summary>
	/// The width one caption's own TEXT needs, with no room counted around it. The caption is the ONE thing
	/// whose preferred size does not depend on the box it sits in (TMP measures the text, not the rect), which is
	/// why a button's width is read from it — an English caption in a button sized for a Chinese one is the
	/// user's report of 2026-10-01 (`Preferences` running into its border).
	/// </summary>
	internal static float CaptionTextWidth(TMP_Text caption)
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

			return widest > 0f ? widest : 0f;
		}

		return values.x > 0f ? values.x : 0f;
	}

	/// <summary>
	/// What a caption needs of its control's width: its own text plus the room the game's own row keeps around
	/// that text, at the size the caption draws at. It is <see cref="OnlineUiRowGeometry"/>'s reading — the width
	/// the game's own prefab declares for a control that IS a button — and the room is added HERE and nowhere
	/// else: the element path adds its own room through <see cref="OnlineUiControlBox.CaptionedWidth"/>, and the
	/// cycle that did both counted it twice, which is the 231.2-unit English `Preferences` tab of batch
	/// `20261015-a` against the 191.2 its text needs.
	/// </summary>
	internal static float CaptionWidth(TMP_Text caption)
	{
		var text = CaptionTextWidth(caption);
		return text > 0f ? text + (2f * caption.fontSize) : 0f;
	}

	/// <summary>
	/// The width a field or a dropdown falls back to when neither the model nor the prefab declares one: a
	/// field is the one control whose content is the player's, so a floor nobody declared is the layout's own
	/// minimum rather than zero.
	/// </summary>
	internal static float WithMinimum(float width) =>
		width > 0f ? width : OnlineUiWindowLayout.MinimumControlWidth;

	/// <summary>Whether the element takes the whole row it is on: a label with no width of its own takes what
	/// is left, and the kinds built on one of the game's own rows (a label with a control) are a row in
	/// themselves.</summary>
	internal static bool FillsTheRow(OnlineUiElementModel element) => element.Kind switch
	{
		OnlineUiElementKind.Label => element.Width <= 0f,
		OnlineUiElementKind.Toggle or OnlineUiElementKind.Dropdown or OnlineUiElementKind.TextField or OnlineUiElementKind.NumberField or OnlineUiElementKind.Slider => true,
		_ => false,
	};
}
