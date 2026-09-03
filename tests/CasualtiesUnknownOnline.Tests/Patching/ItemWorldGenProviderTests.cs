using System;
using System.Collections;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

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

	private static bool TryBind(object provider, string id, ModItemDefinition definition)
	{
		var bind = provider.GetType().GetMethod(
			"TryBind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("TryBind not found.");
		var registration = new ModContentRegistration(
			"mod.a",
			new ModContentDefinition(id, ModContentKind.Item, definition.ToPayload(), 1));
		return (bool)bind.Invoke(provider, [registration])!;
	}

	private static string[] SnapshotIds(object provider)
	{
		var method = provider.GetType().GetMethod(
			"GetDefinitionsForWorldSpawn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("GetDefinitionsForWorldSpawn not found.");
		var snapshot = (IEnumerable)method.Invoke(provider, null)!;
		var ids = new System.Collections.Generic.List<string>();
		foreach (var item in snapshot)
		{
			var key = item.GetType().GetProperty("Key")!.GetValue(item);
			ids.Add((string)key!);
		}

		return [.. ids];
	}

	private static ModItemDefinition ValidItem(float? worldSpawnPerChunk = 0.1f) =>
		new()
		{
			Category = "misc",
			TemplateId = "stone",
			WorldSpawnPerChunk = worldSpawnPerChunk
		};

	[Fact]
	public void GetDefinitionsForWorldSpawn_ReturnsStableIdOrderAndFiltersDisabled()
	{
		var provider = CreateProvider();

		Assert.True(TryBind(provider, "zebra", ValidItem(2f)));
		Assert.True(TryBind(provider, "alpha", ValidItem(1f)));
		Assert.True(TryBind(provider, "disabled", ValidItem(null)));
		Assert.True(TryBind(provider, "zero", ValidItem(0f)));

		Assert.Equal(["alpha", "zebra"], SnapshotIds(provider));
	}

	[Fact]
	public void TryBind_AcceptsValidWorldSpawnAndRejectsInvalidValues()
	{
		var provider = CreateProvider();

		Assert.True(TryBind(provider, "valid", ValidItem(0.5f)));
		Assert.False(TryBind(provider, "negative", ValidItem(-0.1f)));
		Assert.False(TryBind(provider, "nan", ValidItem(float.NaN)));
		Assert.False(TryBind(provider, "inf", ValidItem(float.PositiveInfinity)));
		Assert.False(TryBind(provider, "neginf", ValidItem(float.NegativeInfinity)));
	}
}
