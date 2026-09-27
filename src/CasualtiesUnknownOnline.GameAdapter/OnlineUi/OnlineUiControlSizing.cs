using CasualtiesUnknownOnline.Runtime.OnlineUi;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// How wide a control of the Online UI window has to be (ticket online-ui-layout-and-input-detail-pass, S2 and
/// the layout-engine pass that followed it): the model's width is a FLOOR, and everything above it is what the
/// ENGINE reports for the control's own content — a caption TMP measured, a nested layout group's preferred
/// size, a label's flexible share. CUO declares the floor and reads the answer; it computes no width.
///
/// <para>
/// That split is the point. A hand-computed width misses what the engine knows: the font's fallbacks, a
/// translated caption, a value the player typed, a prefab the game resized. The user's acceptance pass is what
/// settled it — one box clipped an English caption that fitted a Chinese one, and a width computed once cannot
/// notice.
/// </para>
/// </summary>
internal static class OnlineUiControlSizing
{
	/// <summary>
	/// The width the control takes in the row it is laid out in: the engine's own preferred width for it, never
	/// below the floor the model declared. A label is the exception — it takes what the row has left and counts
	/// as nothing towards the wrap, so it reports 0 here.
	/// </summary>
	internal static float EffectiveWidth(OnlineUiElementModel element, RectTransform rect, LayoutElement layout) =>
		element.Kind == OnlineUiElementKind.Label
			? 0f
			: Mathf.Max(layout.minWidth, LayoutUtility.GetPreferredSize(rect, 0));

	/// <summary>Whether the element takes the whole row it is on: a label with no width of its own takes what is
	/// left, and the four kinds built on one of the game's own rows (a label with a control) are a row in
	/// themselves.</summary>
	internal static bool FillsTheRow(OnlineUiElementModel element) => element.Kind switch
	{
		OnlineUiElementKind.Label => element.Width <= 0f,
		OnlineUiElementKind.Toggle or OnlineUiElementKind.Dropdown or OnlineUiElementKind.TextField or OnlineUiElementKind.Slider => true,
		_ => false,
	};
}
