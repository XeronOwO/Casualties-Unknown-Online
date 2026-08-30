using BepInEx.Configuration;
using CasualtiesUnknownOnline.Runtime.OnlineUi;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Owns the BepInEx config entry for the local player's selected marker color.
/// Index -1 means automatic SteamId palette assignment; 0..N-1 selects one of
/// the shared player palette colors. The Runtime identity adapters receive the
/// resolved value through <see cref="PlayerColorValue"/> so handshakes and
/// roster announcements can carry it to peers.
/// </summary>
internal sealed class PlayerColorConfigEditor
{
	private readonly ConfigFile _config;
	private readonly ConfigEntry<int> _colorIndex;

	internal PlayerColorConfigEditor(ConfigFile config, ConfigEntry<int> colorIndex)
	{
		_config = config;
		_colorIndex = colorIndex;
	}

	/// <summary>The configured palette index, or -1 when automatic.</summary>
	internal int ColorIndex => _colorIndex.Value;

	/// <summary>The resolved local player color, or null when automatic.</summary>
	internal PlayerColorValue? CurrentColor =>
		PlayerColorResolver.TryGet(_colorIndex.Value, out var color) ? color : null;

	internal void SetColorIndex(int value)
	{
		_colorIndex.Value = value;
		_config.Save();
	}
}
