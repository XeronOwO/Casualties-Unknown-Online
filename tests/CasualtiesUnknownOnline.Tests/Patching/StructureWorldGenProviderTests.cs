using System;
using System.Collections;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The GameAdapter structure provider's worldgen snapshot contract. The test
/// project never compile-references GameAdapter (it binds game assemblies), so
/// this locks the deterministic enumeration used by
/// <c>StructureWorldGenDistribution</c> reflectively: both peers must iterate
/// the same structures in the same order when consuming the shared generation
/// random stream.
/// </summary>
public class StructureWorldGenProviderTests
{
	private static Type ProviderType => GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterStructureContentProvider",
		throwOnError: true)!;

	private static object CreateProvider()
	{
		var loggerType = typeof(NullLogger<>).MakeGenericType(ProviderType);
		var logger = loggerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
			?? loggerType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
			?? throw new InvalidOperationException("NullLogger.Instance not found.");
		return Activator.CreateInstance(ProviderType, [logger])!;
	}

	private static ModContentRegistration Registration(string id, ModStructureDefinition definition) =>
		new("mod.a", new ModContentDefinition(id, ModContentKind.Structure, definition.ToPayload(), 1));

	private static string[] SnapshotIds(object provider)
	{
		var method = ProviderType.GetMethod(
			"GetCompiledForWorldGen", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("GetCompiledForWorldGen not found.");
		var snapshot = (IEnumerable)method.Invoke(provider, null)!;
		var ids = new System.Collections.Generic.List<string>();
		foreach (var item in snapshot)
		{
			var key = item.GetType().GetProperty("Key")!.GetValue(item);
			ids.Add((string)key!);
		}

		return [.. ids];
	}

	[Fact]
	public void GetCompiledForWorldGen_ReturnsStableIdOrder()
	{
		var provider = CreateProvider();
		var bind = ProviderType.GetMethod(
			"TryBind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("TryBind not found.");

		var zebra = new ModStructureDefinition
		{
			Width = 1,
			Height = 1,
			Rows = ["#"],
			VanillaBlocks = new System.Collections.Generic.Dictionary<string, int> { ["#"] = 5 },
			SpawnCounts = [2]
		};
		var alpha = new ModStructureDefinition
		{
			Width = 1,
			Height = 1,
			Rows = ["#"],
			VanillaBlocks = new System.Collections.Generic.Dictionary<string, int> { ["#"] = 5 },
			SpawnCounts = [1]
		};

		Assert.True((bool)bind.Invoke(provider, [Registration("zebra", zebra)])!);
		Assert.True((bool)bind.Invoke(provider, [Registration("alpha", alpha)])!);

		var ids = SnapshotIds(provider);

		Assert.Equal(["alpha", "zebra"], ids);
	}
}
