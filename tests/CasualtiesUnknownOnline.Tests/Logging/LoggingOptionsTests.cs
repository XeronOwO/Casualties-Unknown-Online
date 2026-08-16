using System;
using System.IO;
using CasualtiesUnknownOnline.Runtime;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Logging;
using ManualLogSource = BepInEx.Logging.ManualLogSource;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Logging;

/// <summary>
/// The configurable log minimum is enforced inside BOTH CUO providers (the
/// factory minimum stays Trace deliberately, so a live options change reaches
/// the sinks without rebuilding the container). Default Information means
/// Debug/Trace stay silent; a hot change to Debug re-enables them.
/// </summary>
public class LoggingOptionsTests
{
	[Fact]
	public void RollingFileProvider_DefaultInformation_SuppressesDebug_WritesInformation()
	{
		var directory = NewLogDirectory();
		var options = new MutableOptionsMonitor<LoggingOptions>(new LoggingOptions());
		using (var provider = new RollingFileLoggerProvider(directory, legacyLogPath: null, options))
		{
			var logger = provider.CreateLogger("Test");
			Assert.False(logger.IsEnabled(LogLevel.Debug), "Debug must be suppressed at the default Information minimum");
			logger.LogDebug("suppressed debug");
			logger.LogInformation("visible info");
		}

		var text = File.ReadAllText(Path.Combine(directory, "latest.log"));
		Assert.DoesNotContain("suppressed debug", text);
		Assert.Contains("visible info", text);
	}

	[Fact]
	public void RollingFileProvider_HotChangeToDebug_WritesDebug()
	{
		var directory = NewLogDirectory();
		var options = new MutableOptionsMonitor<LoggingOptions>(new LoggingOptions());
		using (var provider = new RollingFileLoggerProvider(directory, legacyLogPath: null, options))
		{
			var logger = provider.CreateLogger("Test");

			options.Set(new LoggingOptions { MinimumLevel = LogLevel.Debug });
			Assert.True(logger.IsEnabled(LogLevel.Debug), "the hot change must re-enable Debug without rebuilding the provider");
			logger.LogDebug("visible debug");
		}

		Assert.Contains("visible debug", File.ReadAllText(Path.Combine(directory, "latest.log")));
	}

	[Fact]
	public void BepInExProvider_UsesTheSameConfigurableMinimum()
	{
		var options = new MutableOptionsMonitor<LoggingOptions>(new LoggingOptions());
		using var provider = new BepInExLoggerProvider(new ManualLogSource("test"), options);
		var logger = provider.CreateLogger("Test");

		Assert.False(logger.IsEnabled(LogLevel.Debug));
		Assert.True(logger.IsEnabled(LogLevel.Information));

		options.Set(new LoggingOptions { MinimumLevel = LogLevel.Warning });
		Assert.False(logger.IsEnabled(LogLevel.Information));
		Assert.True(logger.IsEnabled(LogLevel.Warning));
	}

	private static string NewLogDirectory() =>
		Path.Combine(Path.GetTempPath(), "cuo-tests", $"logs-{Guid.NewGuid():N}");

	[Fact]
	public void Bootstrap_ExtraRegistrations_OptionsReplacementReachesTheProvider()
	{
		var directory = NewLogDirectory();
		var options = new MutableOptionsMonitor<LoggingOptions>(new LoggingOptions { MinimumLevel = LogLevel.Debug });
		using var services = CuoBootstrap.BuildServiceProvider(
			new ManualLogSource("test"),
			directory,
			extraRegistrations: s => s.Replace(
				ServiceDescriptor.Singleton<IOptionsMonitor<LoggingOptions>>(options)));

		var provider = services.GetRequiredService<BepInExLoggerProvider>();
		var logger = provider.CreateLogger("Test");

		Assert.True(logger.IsEnabled(LogLevel.Debug),
			"the plugin's config-backed monitor replacement must reach the DI-resolved log provider");
	}
}
