using System;
using BepInEx.Configuration;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Configuration;
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
				"Minimum CUO log level written to BepInEx and latest.log. Information keeps normal play quiet.",
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
		services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(
			new BepInExOptionsMonitor<HostRulesOptions>(
				config,
				() => new HostRulesOptions
				{
					PvpEnabled = pvpEnabled.Value,
					AutoContinue = autoContinue.Value,
					AllowLateJoin = allowLateJoin.Value,
				},
				pvpEnabled.Definition, autoContinue.Definition, allowLateJoin.Definition)));

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
	}

	private static MelLogLevel ParseLogLevel(string text) =>
		Enum.TryParse(text, ignoreCase: true, out MelLogLevel level)
		&& Enum.IsDefined(typeof(MelLogLevel), level)
			? level
			: MelLogLevel.Information;
}
