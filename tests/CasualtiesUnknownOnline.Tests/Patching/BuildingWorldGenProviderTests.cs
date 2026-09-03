using System;
using System.Collections;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

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
		return Activator.CreateInstance(ProviderType, [logger])!;
	}

	private static bool TryBind(object provider, string id, ModBuildingDefinition definition)
	{
		var bind = provider.GetType().GetMethod(
			"TryBind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("TryBind not found.");
		var registration = new ModContentRegistration(
			"mod.a",
			new ModContentDefinition(id, ModContentKind.Building, definition.ToPayload(), 1));
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

	private static ModBuildingDefinition ValidBuilding(
		float? min = 0.01f,
		float? max = 0.05f,
		ModBuildingGenerationStyle style = ModBuildingGenerationStyle.Standard) =>
		new()
		{
			TemplateId = "crate",
			SpawnMinPerChunk = min,
			SpawnMaxPerChunk = max,
			GenerationStyle = style
		};

	[Fact]
	public void GetDefinitionsForWorldGen_ReturnsStableIdOrderAndFiltersDisabled()
	{
		var provider = CreateProvider();

		Assert.True(TryBind(provider, "zebra", ValidBuilding(0.01f, 0.02f)));
		Assert.True(TryBind(provider, "alpha", ValidBuilding(0.01f, 0.02f)));
		Assert.True(TryBind(provider, "disabled", ValidBuilding(null, null)));
		Assert.True(TryBind(provider, "none", ValidBuilding(0.01f, 0.02f, ModBuildingGenerationStyle.None)));

		Assert.Equal(["alpha", "zebra"], SnapshotIds(provider));
	}

	[Fact]
	public void TryBind_AcceptsValidWorldGenAndRejectsInvalidDensity()
	{
		var provider = CreateProvider();

		Assert.True(TryBind(provider, "valid", ValidBuilding()));
		Assert.False(TryBind(provider, "negative", ValidBuilding(-0.1f, 0.1f)));
		Assert.False(TryBind(provider, "nan", ValidBuilding(float.NaN, 0.1f)));
		Assert.False(TryBind(provider, "inf", ValidBuilding(0f, float.PositiveInfinity)));
		Assert.False(TryBind(provider, "min-gt-max", ValidBuilding(0.5f, 0.1f)));
		Assert.False(TryBind(provider, "bad-offset", new ModBuildingDefinition
		{
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

		Assert.True(TryBind(provider, "valid-drop", new ModBuildingDefinition
		{
			TemplateId = "crate",
			DropOnDestroy = [new ModBuildingDrop { ItemId = "scrap", Chance = 0.5f }]
		}));
		Assert.False(TryBind(provider, "empty-item", new ModBuildingDefinition
		{
			TemplateId = "crate",
			DropOnDestroy = [new ModBuildingDrop { ItemId = "" }]
		}));
		Assert.False(TryBind(provider, "chance-gt-1", new ModBuildingDefinition
		{
			TemplateId = "crate",
			DropOnDestroy = [new ModBuildingDrop { ItemId = "scrap", Chance = 1.5f }]
		}));
		Assert.False(TryBind(provider, "bad-condition", new ModBuildingDefinition
		{
			TemplateId = "crate",
			AlwaysDrop = [new ModBuildingDrop { ItemId = "scrap", MinCondition = 0.8f, MaxCondition = 0.2f }]
		}));
	}
}
