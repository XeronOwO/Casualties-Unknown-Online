using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The building-death drop provenance L0 contract: a new CallContext origin,
/// a pure MonoBehaviour marker, and the two patch shapes that create it
/// (BuildingEntity.Update opens the scope around the local death branch;
/// Item.Awake marks the item while the scope is still active). The runtime
/// item-submit path is intentionally unchanged at this stage — the marker is
/// observable but does not yet fold into a trap composite.
/// </summary>
public class BuildingDeathDropProvenanceTests
{
	private static readonly Type Marker = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Items.BuildingDeathDropOrigin",
		throwOnError: true)!;

	private static readonly Type ItemPatches = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.ItemPatches",
		throwOnError: true)!;

	private static readonly Type CallContext = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.CallContext",
		throwOnError: true)!;

	private static readonly Type BuildingEntityPatch = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.BuildingEntityUpdatePatch",
		throwOnError: true)!;

	[Fact]
	public void CallContext_HasTheBuildingDeathDropOrigin()
	{
		var origin = CallContext.GetNestedType("Origin", BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CallContext.Origin not found.");
		Assert.True(Enum.GetNames(origin).Contains("BuildingDeathDrop"),
			$"CallContext.Origin must declare BuildingDeathDrop, got [{string.Join(", ", Enum.GetNames(origin))}]");
	}

	[Fact]
	public void Marker_IsAPureMonoBehaviour()
	{
		Assert.True(Marker.BaseType?.FullName == "UnityEngine.MonoBehaviour",
			$"BuildingDeathDropOrigin must be a MonoBehaviour marker, got base {Marker.BaseType?.FullName}");
		Assert.True(Marker.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Length == 0,
			"BuildingDeathDropOrigin must stay a pure marker — fields would make it stateful.");
	}

	[Fact]
	public void AwakePatch_MarksInsideTheBuildingDeathScope()
	{
		var awakePatch = ItemPatches.GetNestedType("ItemAwakePatch", BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("ItemPatches.ItemAwakePatch not found.");
		var postfix = awakePatch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("ItemPatches.ItemAwakePatch.Postfix not found.");
		var parameters = postfix.GetParameters();
		Assert.True(parameters.Length == 1
			&& parameters[0].Name == "__instance"
			&& parameters[0].ParameterType.FullName == "Item",
			$"Postfix must have exactly one Item __instance, got {parameters.Length} parameter(s)");
	}

	[Fact]
	public void BuildingEntityPatch_PreservesLocalDeathScopeShape()
	{
		var prefix = BuildingEntityPatch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("BuildingEntityUpdatePatch.Prefix not found.");
		Assert.True(prefix.GetParameters().Any(p => p.Name == "__state" && p.ParameterType.IsByRef),
			"Prefix must expose out __state so Postfix can dispose the BuildingDeathDrop scope.");
	}

	[Fact]
	public void PatchInventory_DeclaresTheItemAwakeContract()
	{
		var inventory = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found.");
		var build = inventory.GetMethod("BuildContracts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PatchInventory.BuildContracts not found.");
		var contracts = ((System.Collections.IEnumerable)build.Invoke(null, null)!).Cast<object>().ToList();

		var hasAwake = contracts.Any(c =>
		{
			var type = c.GetType();
			return (type.GetProperty("TargetType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string) == "Item"
				&& (type.GetProperty("MethodName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string) == "Awake";
		});
		Assert.True(hasAwake, "PatchInventory must declare the Item.Awake patch contract.");
	}
}
