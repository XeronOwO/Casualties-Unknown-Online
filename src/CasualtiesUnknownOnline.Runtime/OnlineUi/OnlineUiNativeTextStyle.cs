namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// One text style read from the game's own UI: the font asset it renders with, at what size and in
/// what colour. The font asset is the unknown a decompiled tree cannot answer — it is a serialized
/// prefab property — so the probe reads it from a live row and names it here.
/// </summary>
public sealed record OnlineUiNativeTextStyle(
	string Source,
	string ObjectPath,
	string FontAssetName,
	float FontSize,
	OnlineUiNativeRgba Color,
	int Occurrences);
