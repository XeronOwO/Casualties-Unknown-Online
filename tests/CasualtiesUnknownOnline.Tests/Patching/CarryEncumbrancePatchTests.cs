using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The carry-weight Harmony surface and its multiplier rule. The adapter is
/// compile-excluded (it binds game/Unity assemblies), so the patch shape and
/// the pure multiplier are exercised reflectively through the shared
/// <see cref="GameAssemblyHost"/>.
/// </summary>
public class CarryEncumbrancePatchTests
{
	private static readonly Type Calculator = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.CarriedEncumbranceCalculator",
		throwOnError: true)!;

	private static readonly Type Patch = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.CarryEncumbrancePatch",
		throwOnError: true)!;

	private static float ApplyMultiplier(float full, float multiplier)
	{
		var method = Calculator.GetMethod("ApplyMultiplier", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CarriedEncumbranceCalculator.ApplyMultiplier not found.");
		return (float)method.Invoke(null, [full, multiplier])!;
	}

	[Fact]
	public void ApplyMultiplier_ScalesByHostMultiplier() => Assert.True(Math.Abs(ApplyMultiplier(10f, 0.8f) - 8f) < 0.001f);

	[Fact]
	public void ApplyMultiplier_ClampsNegativeMultiplierToZero() => Assert.Equal(0f, ApplyMultiplier(10f, -1f));

	[Fact]
	public void PatchSurface_TargetsBodyGetTotalEncumberancePostfix()
	{
		var postfix = Patch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("CarryEncumbrancePatch.Postfix not found.");
		var parameters = postfix.GetParameters();
		Assert.True(parameters.Length == 2, $"Postfix must have __instance + ref __result, got {parameters.Length}");
		Assert.True(parameters[0].Name == "__instance" && parameters[0].ParameterType.FullName == "Body",
			$"Postfix parameter 0 must be Body __instance, got {parameters[0]}");
		Assert.True(parameters[1].Name == "__result" && parameters[1].ParameterType.IsByRef && parameters[1].ParameterType.GetElementType() == typeof(float),
			$"Postfix parameter 1 must be ref float __result, got {parameters[1]}");
	}

	[Fact]
	public void PatchInventory_ContainsTheCarryEncumbranceContract()
	{
		var inventory = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found.");
		var build = inventory.GetMethod("BuildContracts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PatchInventory.BuildContracts not found.");
		var contracts = (System.Collections.IEnumerable)build.Invoke(null, null)!;
		var found = contracts.Cast<object>().Any(c =>
		{
			var type = c.GetType();
			var target = type.GetProperty("TargetType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string;
			var method = type.GetProperty("MethodName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string;
			return target == "Body" && method == "GetTotalEncumberance";
		});

		Assert.True(found, "PatchInventory must declare the Body.GetTotalEncumberance patch contract.");
	}
}
