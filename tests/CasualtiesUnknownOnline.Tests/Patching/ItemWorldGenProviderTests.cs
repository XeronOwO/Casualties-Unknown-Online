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
/// The GameAdapter item provider's world-spawn distribution contract. The test
/// project never compile-references GameAdapter (it binds game assemblies), so
/// this locks the stable enumeration and validation used by
/// <c>ItemWorldGenDistribution</c> reflectively: both peers must iterate the
/// same item definitions in the same order when consuming the shared generation
/// random stream, and invalid authored world-spawn values must be refused before
/// they can enter the world.
/// </summary>
[Trait("Category", "Integration")]
public class ItemWorldGenProviderTests
{
	private static Type ProviderType => GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterItemContentProvider",
		throwOnError: true)!;

	private static object CreateProvider()
	{
		var loggerType = typeof(NullLogger<>).MakeGenericType(ProviderType);
		var logger = loggerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
			?? loggerType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
			?? throw new InvalidOperationException("NullLogger.Instance not found.");
		return Activator.CreateInstance(ProviderType, [logger])!;
	}

	private static bool TryBind(object provider, ModItemDefinition definition)
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
			"GetDefinitionsForWorldSpawn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("GetDefinitionsForWorldSpawn not found.");
		var snapshot = (IEnumerable)method.Invoke(provider, null)!;
		var ids = new List<string>();
		foreach (var item in snapshot)
		{
			var key = item.GetType().GetProperty("Key")!.GetValue(item);
			ids.Add((string)key!);
		}

		return [.. ids];
	}

	private static ModItemDefinition ValidItem(string id, float? worldSpawnPerChunk = 0.1f) =>
		new()
		{
			Id = id,
			Category = "misc",
			TemplateId = "stone",
			WorldSpawnPerChunk = worldSpawnPerChunk
		};

	[Fact]
	public void GetDefinitionsForWorldSpawn_ReturnsStableIdOrderAndFiltersDisabled()
	{
		var provider = CreateProvider();

		Assert.True(TryBind(provider, ValidItem("zebra", 2f)));
		Assert.True(TryBind(provider, ValidItem("alpha", 1f)));
		Assert.True(TryBind(provider, ValidItem("disabled", null)));
		Assert.True(TryBind(provider, ValidItem("zero", 0f)));

		Assert.Equal(["alpha", "zebra"], SnapshotIds(provider));
	}

	[Fact]
	public void TryBind_AcceptsValidWorldSpawnAndRejectsInvalidValues()
	{
		var provider = CreateProvider();

		Assert.True(TryBind(provider, ValidItem("valid", 0.5f)));
		Assert.False(TryBind(provider, ValidItem("negative", -0.1f)));
		Assert.False(TryBind(provider, ValidItem("nan", float.NaN)));
		Assert.False(TryBind(provider, ValidItem("inf", float.PositiveInfinity)));
		Assert.False(TryBind(provider, ValidItem("neginf", float.NegativeInfinity)));
	}
}
