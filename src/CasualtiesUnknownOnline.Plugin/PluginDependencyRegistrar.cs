using System;
using BepInEx.Configuration;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Diagnostics;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MelLogLevel = Microsoft.Extensions.Logging.LogLevel;
using GameAdapterImpl = CasualtiesUnknownOnline.GameAdapter.GameAdapter;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The BepInEx config → DI bridge that is owned by the plugin but not by the
/// BepInEx lifecycle class. Keeping it out of <c>Plugin.cs</c> makes the
/// plugin itself a thin BepInEx lifecycle/hotkey shell and leaves the
/// multi-section config registration in one real top-level responsibility.
/// </summary>
internal static class PluginDependencyRegistrar
{
	internal static void Apply(ConfigFile config, IServiceCollection services)
	{
		// BepInEx ConfigFile → IOptionsMonitor bridge: the plugin owns
		// the ConfigEntry declarations, the Runtime owns the options
		// snapshots and the hot-reload monitor. Invalid/out-of-range
		// values are clamped by BepInEx (range) or by the defensive
		// parser below (log level falls back to Information).
		var stateStreamHz = config.Bind("Sync", "StateStreamHz",
			StateStreamOptions.DefaultStateStreamHz,
			new ConfigDescription(
				"Player/enemy state snapshot frequency in Hz (higher = smoother, more bandwidth).",
				new AcceptableValueRange<int>(StateStreamOptions.MinStateStreamHz, StateStreamOptions.MaxStateStreamHz)));
		var minimumLevel = config.Bind("Logging", "MinimumLevel", "Information",
			new ConfigDescription(
				"Minimum CUO log level written to BepInEx and latest.log. Information keeps normal play quiet; Debug enables high-frequency per-frame/per-event traces (clone inventory, character relay, block/sound events).",
				new AcceptableValueList<string>(new[] { "Information", "Trace", "Debug", "Warning", "Error", "Critical", "None" })));
		// Host-authoritative revive/respawn rules (KrokMP-inspired
		// co-op lifecycle). These are read at decision time, so a
		// config edit hot-reloads without a restart.
		var permadeath = config.Bind("Respawn", "Permadeath", false,
			new ConfigDescription("True = death is terminal: no trader revive and no next-level auto-respawn."));
		var reviveFromTrader = config.Bind("Respawn", "ReviveFromTrader", true,
			new ConfigDescription("True = a living player can revive a dead teammate at a friendly trader."));
		var reviveOnNextLevel = config.Bind("Respawn", "ReviveOnNextLevel", true,
			new ConfigDescription("True = dead players are auto-respawned when the host finishes the next world layer."));
		var keepInventory = config.Bind("Respawn", "KeepInventory", true,
			new ConfigDescription("True = auto-respawn keeps the character's carried/worn items."));
		var keepSkills = config.Bind("Respawn", "KeepSkills", true,
			new ConfigDescription("True = auto-respawn keeps skills/experience; false resets them."));
		services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<StateStreamOptions>>(
			new BepInExOptionsMonitor<StateStreamOptions>(
				config,
				() => new StateStreamOptions { StateStreamHz = stateStreamHz.Value },
				stateStreamHz.Definition)));
		services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<LoggingOptions>>(
			new BepInExOptionsMonitor<LoggingOptions>(
				config,
				() => new LoggingOptions { MinimumLevel = ParseLogLevel(minimumLevel.Value) },
				minimumLevel.Definition)));
		services.AddSingleton(new LoggingConfigEditor(config, minimumLevel));
		services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<RespawnOptions>>(
			new BepInExOptionsMonitor<RespawnOptions>(
				config,
				() => new RespawnOptions
				{
					Permadeath = permadeath.Value,
					ReviveFromTrader = reviveFromTrader.Value,
					ReviveOnNextLevel = reviveOnNextLevel.Value,
					RespawnKeepInventory = keepInventory.Value,
					RespawnKeepSkills = keepSkills.Value,
				},
				permadeath.Definition, reviveFromTrader.Definition, reviveOnNextLevel.Definition,
				keepInventory.Definition, keepSkills.Definition)));
		// Minimal host-rules service: the new high-value flags that are
		// not already in [Respawn]. Read at decision time so a config edit
		// hot-reloads without a restart.
		var pvpEnabled = config.Bind("HostRules", "PvpEnabled", false,
			new ConfigDescription("Reserved host-rule surface for a future PVP damage domain; no gameplay effect yet."));
		var autoContinue = config.Bind("HostRules", "AutoContinue", false,
			new ConfigDescription("Reserved host-rule surface for automatic next-layer continuation; not wired yet."));
		var allowLateJoin = config.Bind("HostRules", "AllowLateJoin", true,
			new ConfigDescription("True = a brand-new player may join the host's already-running world."));
		var allowRemoteInventoryTake = config.Bind("HostRules", "AllowRemoteInventoryTake", true,
			new ConfigDescription("True = other players may take carried items from a remote player's inventory (unconscious/dead loot remains the default rule; false disables cross-player inventory take entirely)."));
		var widenRunSettings = config.Bind("HostRules", "WidenRunSettings", true,
			new ConfigDescription("Host-only: widen the native custom run-settings sliders in co-op so the run can be tuned for the actual lobby size. Values still ride the existing world-start params."));
		var piggybackWeight = config.Bind("HostRules", "PiggybackWeightMultiplier", 0.8,
			new ConfigDescription(
				"Host-only: fraction of a carried/rider player's full encumbrance added to the carrier while a carry/piggyback relation is active. 0 disables the movement penalty.",
				new AcceptableValueRange<double>(0.0, 3.0)));
		services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(
			new BepInExOptionsMonitor<HostRulesOptions>(
				config,
				() => new HostRulesOptions
				{
					PvpEnabled = pvpEnabled.Value,
					AutoContinue = autoContinue.Value,
					AllowLateJoin = allowLateJoin.Value,
					AllowRemoteInventoryTake = allowRemoteInventoryTake.Value,
					WidenRunSettings = widenRunSettings.Value,
					PiggybackWeightMultiplier = (float)piggybackWeight.Value,
				},
				pvpEnabled.Definition, autoContinue.Definition, allowLateJoin.Definition,
				allowRemoteInventoryTake.Definition,
				widenRunSettings.Definition, piggybackWeight.Definition)));

		// UI language: en or zh. The localization service normalizes anything
		// starting with "zh" to zh and everything else to English.
		var language = config.Bind("UI", "Language", "en",
			new ConfigDescription(
				"CUO UI language. Supported: en, zh.",
				new AcceptableValueList<string>(new[] { "en", "zh" })));
		services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<LocalizationOptions>>(
			new BepInExOptionsMonitor<LocalizationOptions>(
				config,
				() => new LocalizationOptions { Language = language.Value },
				language.Definition)));
		services.AddSingleton(new LocalizationConfigEditor(config, language));

		// Host-rule write path for the Online UI Admin page. The runtime reads
		// through IOptionsMonitor; this editor holds the ConfigEntry references
		// so the UI can toggle and persist the same entries.
		services.AddSingleton(new HostRulesConfigEditor(
			config,
			pvpEnabled,
			autoContinue,
			allowLateJoin,
			allowRemoteInventoryTake,
			widenRunSettings,
			piggybackWeight,
			permadeath,
			reviveFromTrader,
			reviveOnNextLevel,
			keepInventory,
			keepSkills));

		// IP-direct (non-Steam) connection settings. The custom display name is
		// used only in IP-direct sessions; Steam sessions keep using the Steam
		// persona name. Ports are validated by BepInEx's range.
		var ipListenPort = config.Bind("IpDirect", "ListenPort", 7777,
			new ConfigDescription(
				"TCP port the IP-direct host listens on.",
				new AcceptableValueRange<int>(1, 65535)));
		var ipJoinAddress = config.Bind("IpDirect", "JoinAddress", "127.0.0.1",
			new ConfigDescription("IP address or hostname of an IP-direct host to join."));
		var ipJoinPort = config.Bind("IpDirect", "JoinPort", 7777,
			new ConfigDescription(
				"TCP port of an IP-direct host to join.",
				new AcceptableValueRange<int>(1, 65535)));
		var ipDisplayName = config.Bind("IpDirect", "DisplayName", "",
			new ConfigDescription("Custom in-game display name for IP-direct sessions (empty = player-<id>)."));
		services.AddSingleton(new IpDirectConfigEditor(
			config,
			ipListenPort,
			ipJoinAddress,
			ipJoinPort,
			ipDisplayName));

		// Opt-in hot-path latency instrumentation. Default off: it must not
		// affect normal play; when enabled it only adds a stopwatch per
		// measured domain call and a one-line-per-name summary at the log
		// interval.
		var latencyEnabled = config.Bind("Diagnostics", "LatencyInstrumentation", false,
			new ConfigDescription("True = collect and log per-domain CUO update-pump timing (opt-in, off by default)."));
		var latencyInterval = config.Bind("Diagnostics", "LatencyLogIntervalSeconds", 1.0,
			new ConfigDescription(
				"Seconds between aggregated hot-path latency log lines.",
				new AcceptableValueRange<double>(0.1, 60.0)));
		services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<LatencyOptions>>(
			new BepInExOptionsMonitor<LatencyOptions>(
				config,
				() => new LatencyOptions
				{
					Enabled = latencyEnabled.Value,
					LogIntervalSeconds = Math.Max(0.1, latencyInterval.Value),
				},
				latencyEnabled.Definition, latencyInterval.Definition)));
		services.AddSingleton<LatencyInstrumentation>();

		// Character-data mapping (Mapster). Mapster 6.0.0 core ships
		// IMapper/Mapper — registered directly, no DI package needed
		// (Mapster.DependencyInjection 10.x requires net6+).
		services.AddSingleton<MapsterMapper.IMapper>(
			new MapsterMapper.Mapper(Mapster.TypeAdapterConfig.GlobalSettings));
		services.AddSingleton<GameAdapterImpl>();
		services.AddSingleton<IGameAdapter>(p => p.GetRequiredService<GameAdapterImpl>());
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<GameAdapterImpl>());
		services.Replace(ServiceDescriptor.Singleton<IModEntitySpawner>(p => p.GetRequiredService<GameAdapterImpl>()));
		services.Replace(ServiceDescriptor.Singleton<IModNativeApiProvider>(p => p.GetRequiredService<GameAdapterImpl>()));

		// Persist newly bound configuration entries (e.g. a fresh [UI] Language
		// section on an existing install) so users can see and edit them.
		config.Save();
	}

	private static MelLogLevel ParseLogLevel(string text) =>
		Enum.TryParse(text, ignoreCase: true, out MelLogLevel level)
		&& Enum.IsDefined(typeof(MelLogLevel), level)
			? level
			: MelLogLevel.Information;
}
