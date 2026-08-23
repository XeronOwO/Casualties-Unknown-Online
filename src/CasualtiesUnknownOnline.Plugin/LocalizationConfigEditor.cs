using BepInEx.Configuration;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Writes the CUO UI language config entry from the Online UI. The Runtime
/// <see cref="Runtime.Localization.LocalizationService"/> already hot-reloads
/// from <c>IOptionsMonitor&lt;LocalizationOptions&gt;</c>; this plugin-side editor
/// owns the concrete <see cref="ConfigEntry{T}"/> so the Preferences page can
/// switch the language and persist it immediately.
/// </summary>
internal sealed class LocalizationConfigEditor
{
	private readonly ConfigFile _config;
	private readonly ConfigEntry<string> _language;

	internal LocalizationConfigEditor(ConfigFile config, ConfigEntry<string> language)
	{
		_config = config;
		_language = language;
	}

	internal string Current => _language.Value;

	internal void Set(string value)
	{
		_language.Value = value;
		_config.Save();
	}
}
