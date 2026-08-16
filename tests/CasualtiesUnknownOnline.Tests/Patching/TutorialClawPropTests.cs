using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The tutorial-claw double-give fix is adapter-only (it marks the game's
/// per-side TutorialHandler creations and keeps them out of the shared
/// item/entity domains until a player picks one up), so the L0 contract is
/// the patch surface itself: the marker type exists, the scope origin exists,
/// the two patch shapes are exactly what Harmony needs, and PatchInventory
/// declares both targets. The Runtime pickup half (spawn-then-pickup for an
/// id-less item) is already covered by the existing item simulation/race
/// suites.
/// </summary>
public class TutorialClawPropTests
{
	private static readonly Type Marker = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Tutorial.TutorialClawProp",
		throwOnError: true)!;

	private static readonly Type UpdatePatch = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.TutorialHandlerUpdatePatch",
		throwOnError: true)!;

	private static readonly Type CreatePatch = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.UtilsCreateTutorialPatch",
		throwOnError: true)!;

	private static readonly Type CallContext = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.CallContext",
		throwOnError: true)!;

	private static IEnumerable BuildContracts()
	{
		var inventory = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found.");
		var build = inventory.GetMethod("BuildContracts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PatchInventory.BuildContracts not found.");
		return (IEnumerable)build.Invoke(null, null)!;
	}

	[Fact]
	public void Marker_IsAMonoBehaviour_WithNoFields()
	{
		Assert.True(Marker.BaseType?.FullName == "UnityEngine.MonoBehaviour",
			$"TutorialClawProp must be a MonoBehaviour marker, got base {Marker.BaseType?.FullName}");
		Assert.True(Marker.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Length == 0,
			"TutorialClawProp must stay a pure marker — fields would make it stateful.");
	}

	[Fact]
	public void CallContext_HasTheTutorialClawSpawnOrigin()
	{
		var origin = CallContext.GetNestedType("Origin", BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CallContext.Origin not found.");
		Assert.True(Enum.GetNames(origin).Contains("TutorialClawSpawn"),
			$"CallContext.Origin must declare TutorialClawSpawn, got [{string.Join(", ", Enum.GetNames(origin))}]");
	}

	[Fact]
	public void UpdatePatch_OpensTheScopeInPrefixAndDisposesItInPostfix()
	{
		var prefix = UpdatePatch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("TutorialHandlerUpdatePatch.Prefix not found.");
		Assert.Equal(typeof(void), prefix.ReturnType);
		var prefixParameters = prefix.GetParameters();
		Assert.True(prefixParameters.Length == 1 && prefixParameters[0].Name == "__state" && prefixParameters[0].ParameterType.IsByRef,
			$"Prefix must have exactly one out __state, got {prefixParameters.Length} parameter(s)");

		var postfix = UpdatePatch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("TutorialHandlerUpdatePatch.Postfix not found.");
		var postfixParameters = postfix.GetParameters();
		Assert.True(postfixParameters.Length == 1 && postfixParameters[0].Name == "__state"
			&& postfixParameters[0].ParameterType.FullName == "System.IDisposable",
			$"Postfix must have exactly one IDisposable __state, got {postfixParameters.Length} parameter(s)");
	}

	[Fact]
	public void CreatePatch_MarksOnlyTheResult()
	{
		var postfix = CreatePatch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("UtilsCreateTutorialPatch.Postfix not found.");
		var parameters = postfix.GetParameters();
		Assert.True(parameters.Length == 1 && parameters[0].Name == "__result"
			&& parameters[0].ParameterType.FullName == "UnityEngine.GameObject",
			$"Postfix must have exactly one GameObject __result, got {parameters.Length} parameter(s)");
	}

	[Fact]
	public void PatchInventory_ContainsBothTargets()
	{
		var contracts = BuildContracts().Cast<object>().ToList();
		var hasUpdate = contracts.Any(c =>
		{
			var type = c.GetType();
			return (type.GetProperty("TargetType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string) == "TutorialHandler"
				&& (type.GetProperty("MethodName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string) == "Update";
		});
		Assert.True(hasUpdate, "PatchInventory must declare the TutorialHandler.Update patch contract.");

		var hasCreate = contracts.Any(c =>
		{
			var type = c.GetType();
			var target = type.GetProperty("TargetType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string;
			var method = type.GetProperty("MethodName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string;
			var parameters = type.GetProperty("ParameterTypes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c);
			return target == "Utils" && method == "Create"
				&& parameters is System.Collections.Generic.List<string> names
				&& names.Count == 3
				&& names[0] == "System.String"
				&& names[1] == "UnityEngine.Vector2"
				&& names[2] == "System.Single";
		});
		Assert.True(hasCreate, "PatchInventory must declare the Utils.Create(string, Vector2, float) tutorial patch contract.");
	}
}
