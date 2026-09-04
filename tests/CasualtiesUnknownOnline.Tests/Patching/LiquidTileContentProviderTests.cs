using System;
using System.Collections;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The GameAdapter liquid-tile provider's binding contract. The test project
/// never compile-references GameAdapter (it binds game assemblies), so this
/// locks the stable enumeration and validation used by
/// <c>LiquidTileWorldGenDistribution</c> reflectively: both peers must iterate
/// the same definitions in the same order when consuming the shared generation
/// random stream, and invalid authored numeric fields must be refused before
/// they can enter the fluid grid.
/// </summary>
public class LiquidTileContentProviderTests
{
	private static Type ProviderType => GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterLiquidTileContentProvider",
		throwOnError: true)!;

	private static object CreateProvider()
	{
		var loggerType = typeof(NullLogger<>).MakeGenericType(ProviderType);
		var logger = loggerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
			?? loggerType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
			?? throw new InvalidOperationException("NullLogger.Instance not found.");
		return Activator.CreateInstance(ProviderType, [logger])!;
	}

	private static bool TryBind(object provider, string id, ModLiquidTileDefinition definition)
	{
		var bind = provider.GetType().GetMethod(
			"TryBind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("TryBind not found.");
		var registration = new ModContentRegistration(
			"mod.a",
			new ModContentDefinition(id, ModContentKind.LiquidTile, definition.ToPayload(), 1));
		return (bool)bind.Invoke(provider, [registration])!;
	}

	private static string[] SnapshotIds(object provider)
	{
		var method = provider.GetType().GetMethod(
			"GetDefinitionsForWorldGen", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("GetDefinitionsForWorldGen not found.");
		var snapshot = (IEnumerable)method.Invoke(provider, null)!;
		var ids = new List<string>();
		foreach (var item in snapshot)
		{
			var key = item.GetType().GetProperty("Key")!.GetValue(item);
			ids.Add((string)key!);
		}

		return [.. ids];
	}

	private static ModLiquidTileDefinition ValidTile(float spawnAmount = 0f) =>
		new()
		{
			LiquidId = "water",
			SpawnAmount = spawnAmount,
			SpawnLayers = ModLiquidTileDefinition.AllSpawnLayers,
			MaxFloodFill = 128
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
	public void TryBind_AcceptsValidDefinitionAndRejectsInvalidDrag()
	{
		var provider = CreateProvider();

		var valid = ValidTile();
		var invalid = ValidTile();
		invalid.Drag = 1.5f;

		Assert.True(TryBind(provider, "valid", valid));
		Assert.False(TryBind(provider, "invalid", invalid));
	}
}
