using System;
using System.Collections;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The GameAdapter tile provider's worldgen/drop projection contract. The test
/// project never compile-references GameAdapter (it binds game assemblies), so
/// this locks the stable enumeration and validation used by
/// <c>TileWorldGenDistribution</c> and the custom tile drop path reflectively:
/// both peers must iterate the same tile definitions in the same order when
/// consuming the shared generation random stream, and invalid authored drop
/// values must be refused before they can enter the world.
/// </summary>
public class TileWorldGenProviderTests
{
	private static Type ProviderType => GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterTileContentProvider",
		throwOnError: true)!;

	private static object CreateProvider()
	{
		var loggerType = typeof(NullLogger<>).MakeGenericType(ProviderType);
		var logger = loggerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
			?? loggerType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
			?? throw new InvalidOperationException("NullLogger.Instance not found.");
		return Activator.CreateInstance(ProviderType, [logger])!;
	}

	private static bool TryBind(object provider, string id, ModTileDefinition definition)
	{
		var bind = provider.GetType().GetMethod(
			"TryBind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("TryBind not found.");
		var registration = new ModContentRegistration(
			"mod.a",
			new ModContentDefinition(id, ModContentKind.Tile, definition.ToPayload(), 1));
		return (bool)bind.Invoke(provider, [registration])!;
	}

	private static string[] SnapshotIds(object provider)
	{
		var method = provider.GetType().GetMethod(
			"GetDefinitionsForWorldGen", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("GetDefinitionsForWorldGen not found.");
		var snapshot = (IEnumerable)method.Invoke(provider, null)!;
		var ids = new System.Collections.Generic.List<string>();
		foreach (var item in snapshot)
		{
			var key = item.GetType().GetProperty("Key")!.GetValue(item);
			ids.Add((string)key!);
		}

		return [.. ids];
	}

	private static ModTileDefinition ValidTile(float spawnAmount = 0f) =>
		new()
		{
			TemplateTileIndex = 1,
			SpawnAmount = spawnAmount,
			SpawnLayers = ModTileDefinition.AllSpawnLayers,
			GenerationStyle = ModTileGenerationStyle.Vein
		};

	[Fact]
	public void GetDefinitionsForWorldGen_ReturnsStableIdOrder()
	{
		var provider = CreateProvider();

		Assert.True(TryBind(provider, "zebra", ValidTile(2f)));
		Assert.True(TryBind(provider, "alpha", ValidTile(1f)));

		Assert.Equal(["alpha", "zebra"], SnapshotIds(provider));
	}

	[Fact]
	public void TryBind_AcceptsValidDropAndRejectsInvalidDropChance()
	{
		var provider = CreateProvider();

		var valid = ValidTile();
		valid.Drops = [new ModTileDrop { ItemId = "item", Chance = 0.5f }];
		var invalid = ValidTile();
		invalid.Drops = [new ModTileDrop { ItemId = "item", Chance = 1.5f }];

		Assert.True(TryBind(provider, "valid", valid));
		Assert.False(TryBind(provider, "invalid", invalid));
	}
}
