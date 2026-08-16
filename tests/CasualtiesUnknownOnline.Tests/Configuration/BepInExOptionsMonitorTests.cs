using System;
using System.IO;
using BepInEx.Configuration;
using CasualtiesUnknownOnline.Runtime.Configuration;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Configuration;

/// <summary>
/// The BepInEx ConfigFile → IOptionsMonitor bridge: the monitor reads its
/// snapshot from the bound ConfigEntry and re-reads it when that entry's
/// value changes (the hot-reload path every runtime options consumer depends
/// on).
/// </summary>
public class BepInExOptionsMonitorTests
{
	[Fact]
	public void SettingChanged_ReReadsTheOptionsAndNotifiesListeners()
	{
		var config = CreateConfigFile();
		try
		{
			var entry = config.Bind("Sync", "StateStreamHz", 20, "test");
			using var monitor = new BepInExOptionsMonitor<StateStreamOptions>(
				config,
				() => new StateStreamOptions { StateStreamHz = entry.Value },
				entry.Definition);
			var changes = 0;

			using var _ = monitor.OnChange((value, _) =>
			{
				changes++;
				Assert.Equal(5, value.StateStreamHz);
			});

			entry.Value = 5;

			Assert.Equal(5, monitor.CurrentValue.StateStreamHz);
			Assert.Equal(1, changes);
		}
		finally
		{
			File.Delete(config.ConfigFilePath);
		}
	}

	[Fact]
	public void UnwatchedSettingChanged_DoesNotNotify()
	{
		var config = CreateConfigFile();
		try
		{
			var watched = config.Bind("Sync", "StateStreamHz", 20, "test");
			var other = config.Bind("Logging", "MinimumLevel", "Information", "test");
			using var monitor = new BepInExOptionsMonitor<StateStreamOptions>(
				config,
				() => new StateStreamOptions { StateStreamHz = watched.Value },
				watched.Definition);
			var changes = 0;
			using var _ = monitor.OnChange((_, _) => changes++);

			other.Value = "Debug";

			Assert.Equal(0, changes);
			Assert.Equal(20, monitor.CurrentValue.StateStreamHz);
		}
		finally
		{
			File.Delete(config.ConfigFilePath);
		}
	}

	private static ConfigFile CreateConfigFile()
	{
		var path = Path.Combine(Path.GetTempPath(), "cuo-tests", $"config-{Guid.NewGuid():N}.cfg");
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		return new ConfigFile(path, saveOnInit: true);
	}
}
