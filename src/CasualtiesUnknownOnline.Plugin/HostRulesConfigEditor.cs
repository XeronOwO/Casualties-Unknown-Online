using BepInEx.Configuration;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Writes the host-rule/respawn config entries from the Online UI. The runtime
/// exposes only the read-only <c>IHostRules</c> surface; this plugin-side editor
/// owns the concrete BepInEx <see cref="ConfigEntry{T}"/> references so the
/// Admin page can toggle host rules and persist them immediately.
/// </summary>
internal sealed class HostRulesConfigEditor
{
	private readonly ConfigFile _config;
	private readonly ConfigEntry<bool> _pvp;
	private readonly ConfigEntry<bool> _autoContinue;
	private readonly ConfigEntry<bool> _allowLateJoin;
	private readonly ConfigEntry<bool> _widenRunSettings;
	private readonly ConfigEntry<bool> _permadeath;
	private readonly ConfigEntry<bool> _reviveFromTrader;
	private readonly ConfigEntry<bool> _reviveOnNextLevel;
	private readonly ConfigEntry<bool> _keepInventory;
	private readonly ConfigEntry<bool> _keepSkills;

	internal HostRulesConfigEditor(
		ConfigFile config,
		ConfigEntry<bool> pvp,
		ConfigEntry<bool> autoContinue,
		ConfigEntry<bool> allowLateJoin,
		ConfigEntry<bool> widenRunSettings,
		ConfigEntry<bool> permadeath,
		ConfigEntry<bool> reviveFromTrader,
		ConfigEntry<bool> reviveOnNextLevel,
		ConfigEntry<bool> keepInventory,
		ConfigEntry<bool> keepSkills)
	{
		_config = config;
		_pvp = pvp;
		_autoContinue = autoContinue;
		_allowLateJoin = allowLateJoin;
		_widenRunSettings = widenRunSettings;
		_permadeath = permadeath;
		_reviveFromTrader = reviveFromTrader;
		_reviveOnNextLevel = reviveOnNextLevel;
		_keepInventory = keepInventory;
		_keepSkills = keepSkills;
	}

	internal void SetPvpEnabled(bool value) => Set(_pvp, value);

	internal void SetAutoContinue(bool value) => Set(_autoContinue, value);

	internal void SetAllowLateJoin(bool value) => Set(_allowLateJoin, value);

	internal void SetWidenRunSettings(bool value) => Set(_widenRunSettings, value);

	internal void SetPermadeath(bool value) => Set(_permadeath, value);

	internal void SetReviveFromTrader(bool value) => Set(_reviveFromTrader, value);

	internal void SetReviveOnNextLevel(bool value) => Set(_reviveOnNextLevel, value);

	internal void SetKeepInventory(bool value) => Set(_keepInventory, value);

	internal void SetKeepSkills(bool value) => Set(_keepSkills, value);

	private void Set(ConfigEntry<bool> entry, bool value)
	{
		entry.Value = value;
		_config.Save();
	}
}
