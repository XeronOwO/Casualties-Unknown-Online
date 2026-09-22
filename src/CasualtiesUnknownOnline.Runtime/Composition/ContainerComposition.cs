using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Logging;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ManualLogSource = BepInEx.Logging.ManualLogSource;

namespace CasualtiesUnknownOnline.Runtime.Composition;

/// <summary>
/// The container's own seams: every options monitor's mutable default, the two log
/// sinks and the clock. They own no feature state — they are what the features read
/// — and they register first because the transport's own providers resolve the
/// logger and the options monitor in their constructors.
///
/// <para>
/// The defaults are deliberately replaceable: the production plugin swaps the
/// monitors for the BepInEx config-backed ones through the plugin's extra
/// registrations, tests swap them for mutable monitors, and the providers and
/// stream services resolve the monitor through DI, so one replacement reaches every
/// consumer.
/// </para>
/// </summary>
internal static class ContainerComposition
{
	/// <summary>Registers the options-monitor defaults, the logging providers and the clock.</summary>
	internal static void AddContainerSeams(
		IServiceCollection services,
		ManualLogSource bepinExLogSource,
		string logDirectory,
		string? legacyLogPath)
	{
		services.AddSingleton<IOptionsMonitor<LoggingOptions>>(
			new MutableOptionsMonitor<LoggingOptions>(new LoggingOptions()));
		services.AddSingleton<IOptionsMonitor<StateStreamOptions>>(
			new MutableOptionsMonitor<StateStreamOptions>(new StateStreamOptions()));
		services.AddSingleton<IOptionsMonitor<RespawnOptions>>(
			new MutableOptionsMonitor<RespawnOptions>(new RespawnOptions()));
		services.AddSingleton<IOptionsMonitor<HostRulesOptions>>(
			new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions()));
		services.AddSingleton<IOptionsMonitor<LocalizationOptions>>(
			new MutableOptionsMonitor<LocalizationOptions>(new LocalizationOptions()));
		services.AddSingleton<IOptionsMonitor<SaveOptions>>(
			new MutableOptionsMonitor<SaveOptions>(new SaveOptions()));

		// The logging providers are DI-resolved (registered as ILoggerProvider)
		// rather than captured as instances, so the extraRegistrations options
		// replacement also reaches the log sinks. The factory minimum stays Trace
		// on purpose — the providers enforce the configurable level.
		services.AddSingleton(p => new BepInExLoggerProvider(
			bepinExLogSource, p.GetRequiredService<IOptionsMonitor<LoggingOptions>>()));
		services.AddSingleton<ILoggerProvider>(p => p.GetRequiredService<BepInExLoggerProvider>());
		services.AddSingleton(p => new RollingFileLoggerProvider(
			logDirectory, legacyLogPath, p.GetRequiredService<IOptionsMonitor<LoggingOptions>>()));
		services.AddSingleton<ILoggerProvider>(p => p.GetRequiredService<RollingFileLoggerProvider>());
		services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace));

		// The clock (ITimeSource) is a pure reading point — the domain services
		// derive their throttles/timeouts from it; tests replace it with a
		// virtual clock.
		services.AddSingleton<SystemTimeSource>();
		services.AddSingleton<ITimeSource>(p => p.GetRequiredService<SystemTimeSource>());
	}
}
