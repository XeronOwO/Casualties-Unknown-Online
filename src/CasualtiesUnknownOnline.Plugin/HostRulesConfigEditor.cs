using System;
using System.Globalization;
using BepInEx.Configuration;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Writes the host-rule/respawn config entries from the Online UI's Admin page and the console's
/// <c>/gamerule</c> verb. The runtime exposes only the read-only <c>IHostRules</c> surface; this
/// plugin-side editor owns the concrete BepInEx <see cref="ConfigEntry{T}"/> references so both
/// writers change the same entries and persist them immediately (decision 263's change edge then
/// announces the new set).
/// </summary>
internal sealed class HostRulesConfigEditor : IHostRulesEditor
{
	private readonly ConfigFile _config;
	private readonly ConfigEntry<bool> _pvp;
	private readonly ConfigEntry<bool> _autoContinue;
	private readonly ConfigEntry<bool> _allowLateJoin;
	private readonly ConfigEntry<string> _remoteBackpackAccess;
	private readonly ConfigEntry<bool> _allowMemberCheatVerbs;
	private readonly ConfigEntry<bool> _widenRunSettings;
	private readonly ConfigEntry<double> _piggybackWeight;
	private readonly ConfigEntry<string> _nativeBindingParity;
	private readonly ConfigEntry<bool> _permadeath;
	private readonly ConfigEntry<bool> _reviveFromTrader;
	private readonly ConfigEntry<bool> _reviveOnNextLevel;
	private readonly ConfigEntry<bool> _keepInventory;
	private readonly ConfigEntry<bool> _radiationLineStopsAboveTheFloor;
	private readonly ConfigEntry<bool> _spectatorFreeFly;
	private readonly ConfigEntry<bool> _spectateWhileUnconscious;
	private readonly ConfigEntry<bool> _spectateWhileWaitingToJoin;

	internal HostRulesConfigEditor(
		ConfigFile config,
		ConfigEntry<bool> pvp,
		ConfigEntry<bool> autoContinue,
		ConfigEntry<bool> allowLateJoin,
		ConfigEntry<string> remoteBackpackAccess,
		ConfigEntry<bool> allowMemberCheatVerbs,
		ConfigEntry<bool> widenRunSettings,
		ConfigEntry<double> piggybackWeight,
		ConfigEntry<string> nativeBindingParity,
		ConfigEntry<bool> permadeath,
		ConfigEntry<bool> reviveFromTrader,
		ConfigEntry<bool> reviveOnNextLevel,
		ConfigEntry<bool> keepInventory,
		ConfigEntry<bool> radiationLineStopsAboveTheFloor,
		ConfigEntry<bool> spectatorFreeFly,
		ConfigEntry<bool> spectateWhileUnconscious,
		ConfigEntry<bool> spectateWhileWaitingToJoin)
	{
		_config = config;
		_pvp = pvp;
		_autoContinue = autoContinue;
		_allowLateJoin = allowLateJoin;
		_remoteBackpackAccess = remoteBackpackAccess;
		_allowMemberCheatVerbs = allowMemberCheatVerbs;
		_widenRunSettings = widenRunSettings;
		_piggybackWeight = piggybackWeight;
		_nativeBindingParity = nativeBindingParity;
		_permadeath = permadeath;
		_reviveFromTrader = reviveFromTrader;
		_reviveOnNextLevel = reviveOnNextLevel;
		_keepInventory = keepInventory;
		_radiationLineStopsAboveTheFloor = radiationLineStopsAboveTheFloor;
		_spectatorFreeFly = spectatorFreeFly;
		_spectateWhileUnconscious = spectateWhileUnconscious;
		_spectateWhileWaitingToJoin = spectateWhileWaitingToJoin;
	}

	internal void SetPvpEnabled(bool value) => Set(_pvp, value);

	internal void SetAutoContinue(bool value) => Set(_autoContinue, value);

	internal void SetAllowLateJoin(bool value) => Set(_allowLateJoin, value);

	internal void SetRemoteBackpackAccess(RemoteBackpackAccess value) => Set(_remoteBackpackAccess, RemoteBackpackAccessText.Format(value));

	internal void SetAllowMemberCheatVerbs(bool value) => Set(_allowMemberCheatVerbs, value);

	internal void SetWidenRunSettings(bool value) => Set(_widenRunSettings, value);

	internal void SetPiggybackWeightMultiplier(double value) => Set(_piggybackWeight, value);

	internal void SetNativeBindingParity(NativeBindingParity value) => Set(_nativeBindingParity, NativeBindingParityText.Format(value));

	internal void SetPermadeath(bool value) => Set(_permadeath, value);

	internal void SetReviveFromTrader(bool value) => Set(_reviveFromTrader, value);

	internal void SetReviveOnNextLevel(bool value) => Set(_reviveOnNextLevel, value);

	internal void SetKeepInventory(bool value) => Set(_keepInventory, value);

	internal void SetRadiationLineStopsAboveTheFloor(bool value) => Set(_radiationLineStopsAboveTheFloor, value);

	internal void SetSpectatorFreeFly(bool value) => Set(_spectatorFreeFly, value);

	internal void SetSpectateWhileUnconscious(bool value) => Set(_spectateWhileUnconscious, value);

	internal void SetSpectateWhileWaitingToJoin(bool value) => Set(_spectateWhileWaitingToJoin, value);

	/// <summary>
	/// The console's write seam, keyed by the rule the census classifies. The value is re-read with the
	/// Runtime's own value grammar before anything is written: this seam is public to the composition,
	/// so it must not trust its caller, and the check is what makes "the canonical text" a property of
	/// the write rather than a convention. A rule added to <see cref="HostRuleId"/> needs its entry in
	/// the switch below; the default arm names the rule rather than writing a neighbouring entry.
	/// </summary>
	public bool TrySet(HostRuleId rule, string value, out string? error)
	{
		var row = HostRuleCensus.Of(rule);
		if (!HostRuleValueText.TryParse(row, value, out var canonical, out error))
		{
			return false;
		}

		if (!string.Equals(canonical, value, StringComparison.Ordinal))
		{
			error = $"'{value}' is not the canonical form of {row.Name}.";
			return false;
		}

		Action write = rule switch
		{
			HostRuleId.PvpEnabled => () => Set(_pvp, bool.Parse(value)),
			HostRuleId.AutoContinue => () => Set(_autoContinue, bool.Parse(value)),
			HostRuleId.AllowLateJoin => () => Set(_allowLateJoin, bool.Parse(value)),
			HostRuleId.RemoteBackpackAccess => () => Set(_remoteBackpackAccess, value),
			HostRuleId.AllowMemberCheatVerbs => () => Set(_allowMemberCheatVerbs, bool.Parse(value)),
			HostRuleId.WidenRunSettings => () => Set(_widenRunSettings, bool.Parse(value)),
			HostRuleId.PiggybackWeightMultiplier => () => Set(_piggybackWeight, double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture)),
			HostRuleId.NativeBindingParity => () => Set(_nativeBindingParity, value),
			HostRuleId.SaveInventory => () => Set(_keepInventory, bool.Parse(value)),
			HostRuleId.ReviveFromTrader => () => Set(_reviveFromTrader, bool.Parse(value)),
			HostRuleId.ReviveOnNextLevel => () => Set(_reviveOnNextLevel, bool.Parse(value)),
			HostRuleId.Permadeath => () => Set(_permadeath, bool.Parse(value)),
			HostRuleId.RadiationLineStopsAboveTheFloor => () => Set(_radiationLineStopsAboveTheFloor, bool.Parse(value)),
			HostRuleId.SpectatorFreeFly => () => Set(_spectatorFreeFly, bool.Parse(value)),
			HostRuleId.SpectateWhileUnconscious => () => Set(_spectateWhileUnconscious, bool.Parse(value)),
			HostRuleId.SpectateWhileWaitingToJoin => () => Set(_spectateWhileWaitingToJoin, bool.Parse(value)),
			_ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "host rule has no config entry"),
		};

		write();
		error = null;
		return true;
	}

	private void Set(ConfigEntry<bool> entry, bool value)
	{
		entry.Value = value;
		_config.Save();
	}

	private void Set(ConfigEntry<string> entry, string value)
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
