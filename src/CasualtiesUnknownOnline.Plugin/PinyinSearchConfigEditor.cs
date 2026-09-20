using BepInEx.Configuration;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Writes the pinyin search config entry from the Online UI, and owns the
/// default the entry is first written with. The Runtime side reads the live
/// value through <c>IOptionsMonitor&lt;PinyinSearchOptions&gt;</c> — the static
/// Game Adapter gate is bound to that monitor — so a change here applies to the
/// next search without a restart.
/// </summary>
internal sealed class PinyinSearchConfigEditor
{
	private readonly ConfigFile _config;
	private readonly ConfigEntry<bool> _enabled;

	internal PinyinSearchConfigEditor(ConfigFile config, ConfigEntry<bool> enabled)
	{
		_config = config;
		_enabled = enabled;
	}

	internal bool Current => _enabled.Value;

	/// <summary>
	/// The default for a player who has never touched the setting: a Simplified
	/// Chinese desktop gets pinyin search, every other system language keeps the
	/// native search untouched. BepInEx writes the default into the config file
	/// the first time it is created, so an explicit choice always wins.
	/// </summary>
	internal static bool DefaultEnabled =>
		Application.systemLanguage == SystemLanguage.ChineseSimplified;

	internal void Set(bool value)
	{
		_enabled.Value = value;
		_config.Save();
	}

	internal void Toggle() => Set(!Current);
}
