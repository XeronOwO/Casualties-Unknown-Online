using CasualtiesUnknownOnline.Runtime.OnlineUi;
using TMPro;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// The game's own typography as this surface reads it off the game's own row prefab: the font its labels are
/// set in, THE MATERIAL THAT FONT IS DRAWN WITH, the size a body label takes, and the ladder the Online UI's own text styles are drawn at
/// (<see cref="OnlineUiControlFactory.ReadRowTemplate"/> is what reads it).
///
/// <para>
/// It is a value of its own rather than one view's nested type because four surfaces build their text from
/// the one reading — the window, the two panels and the world-space labels — and the factory that reads it
/// hands the same value to each of them.
/// </para>
///
/// <para>
/// THE MATERIAL IS PART OF THE TYPOGRAPHY, not an afterthought: a <see cref="TextMeshProUGUI"/> created in code takes
/// <c>TMP_Settings.defaultFontAsset</c>'s material, which in this build is not the material the game's own labels draw with — the labels are
/// then present, sized, coloured, active, and INVISIBLE (the panel title read exactly that way on 2026-10-25: text set, font set, alpha 1,
/// 269x24 rect, nothing on the frame). Reading <c>fontSharedMaterial</c> off the game's own label is what makes a programmatic label draw
/// like the game's.
/// </para>
/// </summary>
internal readonly record struct OnlineUiControlTypography(TMP_FontAsset? Font, Material? FontMaterial, GameObject? LabelTemplate, float Size)
{
	/// <summary>The game's own sizes, as the IMGUI theme's four levels expressed them relative to the body
	/// text.</summary>
	internal float SizeOf(OnlineUiTextStyle style) => style switch
	{
		OnlineUiTextStyle.Title => Size + 4f,
		OnlineUiTextStyle.Section => Size - 1f,
		OnlineUiTextStyle.Muted => Size - 2f,
		_ => Size,
	};

	internal static FontStyles WeightOf(OnlineUiTextStyle style) =>
		style is OnlineUiTextStyle.Title or OnlineUiTextStyle.Section ? FontStyles.Bold : FontStyles.Normal;
}
