using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The heater-cook patch surface and its pure rules. The adapter is
/// compile-excluded from the test project (it binds game/Unity assemblies), so
/// the patch shape and the decision surface are exercised reflectively — the
/// same host as the other contract tests. The Runtime half of the channel is
/// covered by ItemCookSimulationTests.
/// </summary>
public class HeaterCookPatchTests
{
	private static readonly Type Patch = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.HeaterCookPatch",
		throwOnError: true)!;

	private static readonly Type Rule = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Items.HeaterCookRule",
		throwOnError: true)!;

	private static object InvokeRule(string method, params object[] args)
	{
		var info = Rule.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException($"HeaterCookRule.{method} not found.");
		return info.Invoke(null, args)!;
	}

	[Fact]
	public void Rule_CandidateMatchesTheGamePredicate()
	{
		Assert.True((bool)InvokeRule("IsCookCandidate", true, true, "meat"), "cooker + meat tag + raw id must cook");
		Assert.False((bool)InvokeRule("IsCookCandidate", false, true, "meat"), "a non-cooker Heater must not cook");
		Assert.False((bool)InvokeRule("IsCookCandidate", true, false, "meat"), "a non-meat item must not cook");
		Assert.False((bool)InvokeRule("IsCookCandidate", true, true, "steak"), "steak must not cook again");
	}

	[Fact]
	public void Rule_CookedCondition_IsThirtyPercent()
	{
		var cooked = (float)InvokeRule("CookedCondition", 0.9f);
		Assert.True(Math.Abs(cooked - 0.27f) < 0.0001f, $"0.9 meat must cook to 0.27 steak, got {cooked}");

		Assert.True((bool)InvokeRule("IsCookedCondition", 0.27f, 0.9f), "the native product must match");
		Assert.False((bool)InvokeRule("IsCookedCondition", 0.9f, 0.9f), "a raw copy at the spawn point is not the created steak");
	}

	[Fact]
	public void Rule_SpawnMatch_UsesTheCapturedRawPosition()
	{
		Assert.True((bool)InvokeRule("IsCookedSpawnAt", 10.1f, 20.2f, 10f, 20f), "same-callback spawn within 0.5 units must match");
		Assert.False((bool)InvokeRule("IsCookedSpawnAt", 11f, 20f, 10f, 20f), "a steak 1 unit away is not the created one");
	}

	[Fact]
	public void PatchSurface_PrefixMatchesHeaterSignatureAndCarriesState()
	{
		var prefix = Patch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("HeaterCookPatch.Prefix not found.");
		Assert.Equal(typeof(bool), prefix.ReturnType);
		var parameters = prefix.GetParameters();
		Assert.True(parameters.Length == 3, $"Prefix must have __instance, collision and __state, got {parameters.Length}");
		Assert.True(parameters[0].Name == "__instance" && parameters[0].ParameterType.FullName == "Heater",
			$"Prefix parameter 0 must be Heater __instance, got {parameters[0]}");
		Assert.True(parameters[1].Name == "collision" && parameters[1].ParameterType.FullName == "UnityEngine.Collision2D",
			$"Prefix parameter 1 must be the name-matched collision, got {parameters[1]}");
		Assert.True(parameters[2].Name == "__state" && parameters[2].ParameterType.IsByRef,
			$"Prefix parameter 2 must be the out __state, got {parameters[2]}");
	}

	[Fact]
	public void PatchSurface_PostfixCarriesThePerCallState()
	{
		var postfix = Patch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("HeaterCookPatch.Postfix not found.");
		var parameters = postfix.GetParameters();
		Assert.True(parameters.Length == 2, $"Postfix must have collision + __state, got {parameters.Length}");
		Assert.True(parameters[0].Name == "collision" && parameters[0].ParameterType.FullName == "UnityEngine.Collision2D",
			$"Postfix parameter 0 must be the name-matched collision, got {parameters[0]}");
		Assert.True(parameters[1].Name == "__state" && parameters[1].ParameterType.Name == "CookState",
			$"Postfix parameter 1 must be the per-call CookState, got {parameters[1]}");
	}

	[Fact]
	public void PatchInventory_ContainsTheHeaterCookContract()
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
			return target == "Heater" && method == "OnCollisionEnter2D";
		});

		Assert.True(found, "PatchInventory must declare the Heater.OnCollisionEnter2D patch contract.");
	}
}
