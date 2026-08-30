using System;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using CasualtiesUnknownOnline.Runtime.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Configuration;

/// <summary>
/// The named full-config template store: save/apply/delete/list lifecycle,
/// hot-reload through BepInEx entry changes, stale-entry tolerance and
/// corruption failure behavior.
/// </summary>
public sealed class ConfigurationProfileStoreTests
{
	[Fact]
	public void SaveCurrent_ThenApply_RestoresAllBoundEntries()
	{
		var root = CreateRoot();
		try
		{
			var config = CreateConfig(root);
			var store = CreateStore(root, config);
			var language = config.Bind("UI", "Language", "en", "lang");
			var level = config.Bind("Logging", "MinimumLevel", "Information", "level");
			var hz = config.Bind("Sync", "StateStreamHz", 20, "hz");

			Assert.True(store.TrySaveCurrent("coop", out var error), error);
			language.Value = "zh";
			level.Value = "Debug";
			hz.Value = 5;

			Assert.True(store.TryApply("coop", out error), error);

			Assert.Equal("en", language.Value);
			Assert.Equal("Information", level.Value);
			Assert.Equal(20, hz.Value);
			Assert.Contains("coop", store.ListProfiles());
		}
		finally
		{
			Cleanup(root);
		}
	}

	[Fact]
	public void Apply_SkipsEntriesThatNoLongerExistInCurrentConfig()
	{
		var root = CreateRoot();
		try
		{
			var config = CreateConfig(root);
			var store = CreateStore(root, config);
			var existing = config.Bind("A", "Value", 1, "existing");
			var removed = config.Bind("B", "Value", "old", "removed");

			Assert.True(store.TrySaveCurrent("t", out var error), error);
			Assert.True(config.Remove(removed.Definition));

			existing.Value = 2;

			Assert.True(store.TryApply("t", out error), error);
			Assert.Equal(1, existing.Value);
		}
		finally
		{
			Cleanup(root);
		}
	}

	[Fact]
	public void Apply_TriggersBepInExOptionsMonitorHotReload()
	{
		var root = CreateRoot();
		try
		{
			var config = CreateConfig(root);
			var store = CreateStore(root, config);
			var hz = config.Bind("Sync", "StateStreamHz", 20, "hz");
			using var monitor = new BepInExOptionsMonitor<StateStreamOptions>(
				config,
				() => new StateStreamOptions { StateStreamHz = hz.Value },
				hz.Definition);
			var changes = 0;
			using var _ = monitor.OnChange((value, _) =>
			{
				changes++;
				Assert.Equal(20, value.StateStreamHz);
			});

			Assert.True(store.TrySaveCurrent("hot", out var error), error);
			hz.Value = 5;
			Assert.Equal(5, monitor.CurrentValue.StateStreamHz);

			Assert.True(store.TryApply("hot", out error), error);

			Assert.Equal(20, hz.Value);
			Assert.Equal(20, monitor.CurrentValue.StateStreamHz);
			Assert.Equal(2, changes); // manual set + profile apply both hot-reload the monitor
		}
		finally
		{
			Cleanup(root);
		}
	}

	[Fact]
	public void ListProfiles_IsSortedAndDeleteRemoves()
	{
		var root = CreateRoot();
		try
		{
			var config = CreateConfig(root);
			var store = CreateStore(root, config);
			config.Bind("A", "Value", 1, "one");

			Assert.True(store.TrySaveCurrent("zeta", out var error), error);
			Assert.True(store.TrySaveCurrent("alpha", out error), error);

			Assert.Equal(new[] { "alpha", "zeta" }, store.ListProfiles());

			Assert.True(store.TryDelete("alpha", out error), error);
			Assert.Equal(new[] { "zeta" }, store.ListProfiles());
			Assert.False(store.TryDelete("missing", out _));
		}
		finally
		{
			Cleanup(root);
		}
	}

	[Fact]
	public void InvalidProfileNames_AreRejected()
	{
		var root = CreateRoot();
		try
		{
			var config = CreateConfig(root);
			var store = CreateStore(root, config);
			config.Bind("A", "Value", 1, "one");

			Assert.False(store.TrySaveCurrent("", out _));
			Assert.False(store.TrySaveCurrent("bad/name", out _));
			Assert.False(store.TryApply("..", out _));
			Assert.False(store.TryDelete("bad\\name", out _));
		}
		finally
		{
			Cleanup(root);
		}
	}

	[Fact]
	public void Apply_CorruptProfile_ReturnsError()
	{
		var root = CreateRoot();
		try
		{
			var config = CreateConfig(root);
			var store = CreateStore(root, config);
			config.Bind("A", "Value", 1, "one");
			var badPath = Path.Combine(root, "bad" + ConfigurationProfileStore.ProfileFileExtension);
			File.WriteAllText(badPath, "not a protobuf profile");

			Assert.False(store.TryApply("bad", out var error));
			Assert.Contains("bad", error);
		}
		finally
		{
			Cleanup(root);
		}
	}

	private static ConfigurationProfileStore CreateStore(string root, ConfigFile config) =>
		new(config, root, NullLogger<ConfigurationProfileStore>.Instance);

	private static ConfigFile CreateConfig(string root)
	{
		var path = Path.Combine(root, "live.cfg");
		return new ConfigFile(path, saveOnInit: true);
	}

	private static string CreateRoot()
	{
		var root = Path.Combine(Path.GetTempPath(), "cuo-tests", $"profiles-{Guid.NewGuid():N}");
		Directory.CreateDirectory(root);
		return root;
	}

	private static void Cleanup(string root)
	{
		if (!Directory.Exists(root))
		{
			return;
		}

		foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
		{
			File.Delete(file);
		}

		foreach (var directory in Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
			.OrderByDescending(d => d.Length))
		{
			Directory.Delete(directory);
		}

		Directory.Delete(root);
	}
}
