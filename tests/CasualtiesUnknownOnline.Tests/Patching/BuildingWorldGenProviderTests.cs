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
/// The GameAdapter building provider's world-generation and drop contract. The
/// test project never compile-references GameAdapter (it binds game assemblies),
/// so this locks the stable enumeration and validation used by
/// <c>BuildingWorldGenDistribution</c> reflectively: both peers must iterate
/// the same building definitions in the same order when consuming the shared
/// generation random stream, and invalid authored density/drop values must be
/// refused before they can enter the world.
/// </summary>
[Trait("Category", "Integration")]
public class BuildingWorldGenProviderTests
{
	private static Type ProviderType => GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterBuildingContentProvider",
		throwOnError: true)!;

	private static object CreateProvider()
	{
		var loggerType = typeof(NullLogger<>).MakeGenericType(ProviderType);
		var logger = loggerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
			?? loggerType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
			?? throw new InvalidOperationException("NullLogger.Instance not found.");
		var buildingRuntime = new ModBuildingRuntimeStore(NullLogger<ModBuildingRuntimeStore>.Instance);
		return Activator.CreateInstance(ProviderType, [logger, buildingRuntime])!;
	}

	private static bool TryBind(object provider, ModBuildingDefinition definition)
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

	private static ModBuildingDefinition ValidBuilding(
		string id,
		float? min = 0.01f,
		float? max = 0.05f,
		ModBuildingGenerationStyle style = ModBuildingGenerationStyle.Standard) =>
		new()
		{
			Id = id,
			TemplateId = "crate",
			SpawnMinPerChunk = min,
			SpawnMaxPerChunk = max,
			GenerationStyle = style
		};

	[Fact]
	public void GetDefinitionsForWorldGen_ReturnsStableIdOrderAndFiltersDisabled()
	{
		var provider = CreateProvider();

		Assert.True(TryBind(provider, ValidBuilding("zebra", 0.01f, 0.02f)));
		Assert.True(TryBind(provider, ValidBuilding("alpha", 0.01f, 0.02f)));
		Assert.True(TryBind(provider, ValidBuilding("disabled", null, null)));
		Assert.True(TryBind(provider, ValidBuilding("none", 0.01f, 0.02f, ModBuildingGenerationStyle.None)));

		Assert.Equal(["alpha", "zebra"], SnapshotIds(provider));
	}

	[Fact]
	public void TryBind_AcceptsValidWorldGenAndRejectsInvalidDensity()
	{
		var provider = CreateProvider();

		Assert.True(TryBind(provider, ValidBuilding("valid")));
		Assert.False(TryBind(provider, ValidBuilding("negative", -0.1f, 0.1f)));
		Assert.False(TryBind(provider, ValidBuilding("nan", float.NaN, 0.1f)));
		Assert.False(TryBind(provider, ValidBuilding("inf", 0f, float.PositiveInfinity)));
		Assert.False(TryBind(provider, ValidBuilding("min-gt-max", 0.5f, 0.1f)));
		Assert.False(TryBind(provider, new ModBuildingDefinition
		{
			Id = "bad-offset",
			TemplateId = "crate",
			SurfaceOffset = -1f,
			GenerationStyle = ModBuildingGenerationStyle.Standard,
			SpawnMinPerChunk = 0.01f,
			SpawnMaxPerChunk = 0.02f
		}));
	}

	[Fact]
	public void TryBind_RejectsInvalidDrops()
	{
		var provider = CreateProvider();

		Assert.True(TryBind(provider, new ModBuildingDefinition
		{
			Id = "valid-drop",
			TemplateId = "crate",
			DropOnDestroy = [new ModBuildingDrop { ItemId = "scrap", Chance = 0.5f }]
		}));
		Assert.False(TryBind(provider, new ModBuildingDefinition
		{
			Id = "empty-item",
			TemplateId = "crate",
			DropOnDestroy = [new ModBuildingDrop { ItemId = "" }]
		}));
		Assert.False(TryBind(provider, new ModBuildingDefinition
		{
			Id = "chance-gt-1",
			TemplateId = "crate",
			DropOnDestroy = [new ModBuildingDrop { ItemId = "scrap", Chance = 1.5f }]
		}));
		Assert.False(TryBind(provider, new ModBuildingDefinition
		{
			Id = "bad-condition",
			TemplateId = "crate",
			AlwaysDrop = [new ModBuildingDrop { ItemId = "scrap", MinCondition = 0.8f, MaxCondition = 0.2f }]
		}));
	}
}
