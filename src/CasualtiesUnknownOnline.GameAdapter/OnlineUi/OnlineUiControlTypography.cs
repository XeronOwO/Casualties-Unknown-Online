using CasualtiesUnknownOnline.Runtime.OnlineUi;
using TMPro;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// The game's own typography as this surface reads it off the game's own row prefab: the font its labels are
/// set in, the size a body label takes, and the ladder the Online UI's own text styles are drawn at
/// (<see cref="OnlineUiControlFactory.ReadRowTemplate"/> is what reads it).
///
/// <para>
/// It is a value of its own rather than one view's nested type because four surfaces build their text from
/// the one reading — the window, the two panels and the world-space labels — and the factory that reads it
/// hands the same value to each of them.
/// </para>
/// </summary>
internal readonly record struct OnlineUiControlTypography(TMP_FontAsset? Font, float Size)
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
