using BepInEx.Configuration;

namespace CasualtiesUnknownOnline.PinyinSearch.Core;

/// <summary>
/// The mod's ONE switch, owned by the mod: BepInEx writes it into
/// <c>BepInEx/config/CasualtiesUnknownOnline.PinyinSearch.cfg</c> and the
/// plug-in shell creates the entry in its Awake. Both surfaces read it LIVE —
/// the crafting patch's decision and the console's completion stage — so an edit
/// applies to the next search without a restart, and one entry covers both.
///
/// Unbound means disabled: the game's own substring rule stands and the console
/// keeps its catalog ranking, which is exactly the state before the shell has
/// run.
/// </summary>
internal static class PinyinSearchConfig
{
	private const string Section = "General";
	private const string Key = "Enabled";

	/// <summary>
	/// The description a player reads in the config file. Chinese is the
	/// audience this feature exists for, so the line carries both languages
	/// instead of shipping a localization system for one sentence.
	/// </summary>
	private const string Description =
		"Also match Chinese names by pinyin in the game's crafting search box, and in the console's "
		+ "resource-id completion when CUO is installed. Applies to the next search (no restart). "
		+ "让合成搜索框（以及装了 CUO 时的控制台资源 id 补全）按拼音匹配中文名称——全拼、首字母、"
		+ "单音节或中英混合。改动立即生效，无需重启。";

	private static ConfigEntry<bool>? _enabled;

	/// <summary>Whether pinyin matching extends the native rules right now.</summary>
	internal static bool IsEnabled => _enabled?.Value ?? false;

	/// <summary>
	/// Creates (or re-reads) the entry. BepInEx writes the default the first time
	/// the file is created, so an explicit player choice always wins.
	/// </summary>
	internal static ConfigEntry<bool> Bind(ConfigFile config, bool defaultEnabled) =>
		_enabled = config.Bind(Section, Key, defaultEnabled, Description);
}
