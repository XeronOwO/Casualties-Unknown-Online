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
[Trait("Category", "Integration")]
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

	private static bool TryBind(object provider, ModLiquidTileDefinition definition)
	{
		var bind = provider.GetType().GetMethod(
			"TryBind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("TryBind not found.");
		var registration = new ModContentRegistration("mod.a", definition);
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

	private static ModLiquidTileDefinition ValidTile(string id, float spawnAmount = 0f) =>
		new()
		{
			Id = id,
			LiquidId = "water",
			SpawnAmount = spawnAmount,
			SpawnLayers = ModLiquidTileDefinition.AllSpawnLayers,
			MaxFloodFill = 128
		};

	private static object BoundView(object provider, string id)
	{
		var method = provider.GetType().GetMethod(
			"TryGetDefinition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("TryGetDefinition not found.");
		object?[] arguments = [id, null];
		Assert.True((bool)method.Invoke(provider, arguments)!);
		return arguments[1]!;
	}

	private static object? Member(object target, string name) =>
		target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target);

	[Fact]
	public void GetDefinitionsForWorldGen_ReturnsStableIdOrder()
	{
		var provider = CreateProvider();

		Assert.True(TryBind(provider, ValidTile("zebra", 2f)));
		Assert.True(TryBind(provider, ValidTile("alpha", 1f)));

		Assert.Equal(["alpha", "zebra"], SnapshotIds(provider));
	}

	[Fact]
	public void TryBind_AcceptsValidDefinitionAndRejectsInvalidDrag()
	{
		var provider = CreateProvider();

		var valid = ValidTile("valid");
		var invalid = ValidTile("invalid");
		invalid.Drag = 1.5f;

		Assert.True(TryBind(provider, valid));
		Assert.False(TryBind(provider, invalid));
	}

	/// <summary>
	/// The fluid grid's own defaults, and where they live. A declaration may leave
	/// the liquid id, the fill liquid, the flood-fill budget, the visual base and
	/// the consume-on-drink flag out or out of range, and the grid is built from
	/// the values CUO supplies for them — but the author's object is a contract the
	/// framework READS, and an implementation may compute its members, so the
	/// defaults belong on the provider's own view and never on the declaration.
	/// </summary>
	[Fact]
	public void TryBind_AppliesTheGridsDefaultsToAViewAndLeavesTheDeclarationAlone()
	{
		var provider = CreateProvider();
		var declaration = new ModLiquidTileDefinition
		{
			Id = "raw.tile",
			LiquidId = "",
			FillLiquidId = "",
			MaxFloodFill = 0,
			VisualLiquidByte = 0,
			ConsumeOnDrink = false
		};

		Assert.True(TryBind(provider, declaration));

		var view = BoundView(provider, "raw.tile");
		Assert.Equal("raw.tile", Member(view, "LiquidId"));
		Assert.Equal("raw.tile", Member(view, "FillLiquidId"));
		Assert.Equal(1, (int)Member(view, "MaxFloodFill")!);
		Assert.Equal(1, (int)Member(view, "VisualLiquidByte")!);
		Assert.True((bool)Member(view, "ConsumeOnDrink")!);

		Assert.Equal("", declaration.LiquidId);
		Assert.Equal("", declaration.FillLiquidId);
		Assert.Equal(0, declaration.MaxFloodFill);
		Assert.Equal(0, declaration.VisualLiquidByte);
		Assert.False(declaration.ConsumeOnDrink);
	}
}
