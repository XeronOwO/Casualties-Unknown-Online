using System.Globalization;
using BepInEx.Configuration;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Writes the host-rule/respawn config entries from the Online UI. The runtime
/// exposes only the read-only <c>IHostRules</c> surface; this plugin-side editor
/// owns the concrete BepInEx <see cref="ConfigEntry{T}"/> references so the
/// Admin page can toggle host rules and persist them immediately.
/// </summary>
internal sealed class HostRulesConfigEditor : IHostRulesEditor
{
	private readonly ConfigFile _config;
	private readonly ConfigEntry<bool> _pvp;
	private readonly ConfigEntry<bool> _autoContinue;
	private readonly ConfigEntry<bool> _allowLateJoin;
	private readonly ConfigEntry<bool> _allowRemoteInventoryTake;
	private readonly ConfigEntry<bool> _widenRunSettings;
	private readonly ConfigEntry<double> _piggybackWeight;
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
		ConfigEntry<bool> allowRemoteInventoryTake,
		ConfigEntry<bool> widenRunSettings,
		ConfigEntry<double> piggybackWeight,
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
		_allowRemoteInventoryTake = allowRemoteInventoryTake;
		_widenRunSettings = widenRunSettings;
		_piggybackWeight = piggybackWeight;
		_permadeath = permadeath;
		_reviveFromTrader = reviveFromTrader;
		_reviveOnNextLevel = reviveOnNextLevel;
		_keepInventory = keepInventory;
		_keepSkills = keepSkills;
	}

	internal void SetPvpEnabled(bool value) => Set(_pvp, value);

	internal void SetAutoContinue(bool value) => Set(_autoContinue, value);

	internal void SetAllowLateJoin(bool value) => Set(_allowLateJoin, value);

	internal void SetAllowRemoteInventoryTake(bool value) => Set(_allowRemoteInventoryTake, value);

	internal void SetWidenRunSettings(bool value) => Set(_widenRunSettings, value);

	internal void SetPiggybackWeightMultiplier(double value) => Set(_piggybackWeight, value);

	internal void SetPermadeath(bool value) => Set(_permadeath, value);

	internal void SetReviveFromTrader(bool value) => Set(_reviveFromTrader, value);

	internal void SetReviveOnNextLevel(bool value) => Set(_reviveOnNextLevel, value);

	internal void SetKeepInventory(bool value) => Set(_keepInventory, value);

	internal void SetKeepSkills(bool value) => Set(_keepSkills, value);

	public bool TrySet(string property, string value, out string? error)
	{
		switch (property.Trim().ToLowerInvariant())
		{
			case "pvpenabled":
				return TrySetBool(_pvp, value, out error);
			case "autocontinue":
				return TrySetBool(_autoContinue, value, out error);
			case "allowlatejoin":
				return TrySetBool(_allowLateJoin, value, out error);
			case "allowremoteinventorytake":
				return TrySetBool(_allowRemoteInventoryTake, value, out error);
			case "widenrunsettings":
				return TrySetBool(_widenRunSettings, value, out error);
			case "piggybackweightmultiplier":
				return TrySetDouble(_piggybackWeight, value, out error);
			case "permadeath":
				return TrySetBool(_permadeath, value, out error);
			case "revivefromtrader":
				return TrySetBool(_reviveFromTrader, value, out error);
			case "reviveonnextlevel":
				return TrySetBool(_reviveOnNextLevel, value, out error);
			case "keepinventory":
				return TrySetBool(_keepInventory, value, out error);
			case "keepskills":
				return TrySetBool(_keepSkills, value, out error);
			default:
				error = $"Unknown host-rule property '{property}'.";
				return false;
		}
	}

	private bool TrySetBool(ConfigEntry<bool> entry, string value, out string? error)
	{
		if (!bool.TryParse(value, out var parsed))
		{
			error = $"'{value}' is not a boolean.";
			return false;
		}

		Set(entry, parsed);
		error = null;
		return true;
	}

	private bool TrySetDouble(ConfigEntry<double> entry, string value, out string? error)
	{
		if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
		{
			error = $"'{value}' is not a number.";
			return false;
		}

		Set(entry, parsed);
		error = null;
		return true;
	}

	private void Set(ConfigEntry<bool> entry, bool value)
	{
		entry.Value = value;
		_config.Save();
	}

	private void Set(ConfigEntry<double> entry, double value)
	{
		entry.Value = value;
		_config.Save();
	}
}
