using System;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Patching;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The gun-state report surface: GunScript transitions (fire, rack, safety,
/// load, unload and the Update-driven auto-rack steps) are now reported through
/// the existing item-use fact path via <c>GunStateSync</c>. The adapter is
/// compile-excluded from the test project, so this locks the reflective shape
/// and the patch-contract coverage.
/// </summary>
public class GunStatePatchTests
{
	private static readonly Type Sync = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Items.GunStateSync",
		throwOnError: true)!;

	[Fact]
	public void GunStateSync_HasTryReportWithGunScript()
	{
		var method = Sync.GetMethod("TryReport", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("GunStateSync.TryReport not found.");
		var parameters = method.GetParameters();
		Assert.True(parameters.Length == 1 && parameters[0].ParameterType.Name == "GunScript",
			$"TryReport must take exactly one GunScript, got {parameters.Length} parameter(s)");
		Assert.False(method.IsStatic, "TryReport must be an instance method (the last-reported snapshots belong to the sync domain).");
	}

	[Fact]
	public void GunStatePatchSet_CoversEveryPersistentTransition()
	{
		var contracts = BuildContracts();
		var expected = new[]
		{
			("GunScript", "Update"),
			("GunScript", "Fire"),
			("GunScript", "TryRack"),
			("GunScript", "ToggleSafety"),
			("GunScript", "LoadMag"),
			("GunScript", "UnloadMag"),
		};

		var missing = expected
			.Where(e => !contracts.Any(c => c.TargetType == e.Item1 && c.MethodName == e.Item2))
			.Select(e => $"{e.Item1}.{e.Item2}")
			.ToList();
		Assert.True(missing.Count == 0,
			$"gun-state patch surface is incomplete ({missing.Count}):\n" + string.Join("\n", missing));
	}

	private static System.Collections.Generic.List<PatchContract> BuildContracts()
	{
		var inventory = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found.");
		var build = inventory.GetMethod("BuildContracts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PatchInventory.BuildContracts not found.");
		return (System.Collections.Generic.List<PatchContract>)build.Invoke(null, null)!;
	}
}
