namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// One <c>Image</c> style read from the game's own UI: where it was found, the sprite it draws, how
/// that sprite is sliced and how the graphic is coloured. <see cref="Occurrences"/> is the census's
/// own count — the adapter reports each candidate once and
/// <see cref="OnlineUiNativeStyleCensus"/> folds the duplicates.
/// </summary>
public sealed record OnlineUiNativeImageStyle(
	string Source,
	string ObjectPath,
	string SpriteName,
	string ImageType,
	float PixelsPerUnitMultiplier,
	float BorderLeft,
	float BorderBottom,
	float BorderRight,
	float BorderTop,
	OnlineUiNativeRgba Color,
	int Occurrences)
{
	/// <summary>True when the sprite carries a 9-slice border — the fact a hand-rolled panel cannot guess.</summary>
	public bool IsNineSliced =>
		BorderLeft > 0f || BorderBottom > 0f || BorderRight > 0f || BorderTop > 0f;
}
