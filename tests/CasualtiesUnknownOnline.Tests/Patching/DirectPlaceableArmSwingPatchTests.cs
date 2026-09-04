using System;
using System.Reflection;
using Xunit;
using System.Collections;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The direct placeable-item arm-swing chain: the Body.UseItem and
/// Body.UseItemInHand hooks report a successful
/// scrapmetal/climbingrope/scaffoldingpack placement so the peers' clones
/// replay ArmsSwing through the existing IsAttacking/SwingSeq stream. The pure
/// success rule is exercised directly; the patch surface and the patch-contract
/// declaration are locked reflectively (the adapter is compile-excluded from
/// the test project).
/// </summary>
public class DirectPlaceableArmSwingPatchTests
{
	private static readonly Type BodyItemPatches = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.BodyItemPatches",
		throwOnError: true)!;

	private static readonly Type Policy = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Items.DirectPlaceableArmSwingPolicy",
		throwOnError: true)!;

	[Fact]
	public void Policy_ReportsOnlySuccessfulDirectPlaceableUses()
	{
		var should = Policy.GetMethod("ShouldReport", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("DirectPlaceableArmSwingPolicy.ShouldReport not found.");

		Assert.True((bool)should.Invoke(null, ["scrapmetal", 1f, 0.75f])!,
			"scrapmetal placement must report after its condition cost");
		Assert.True((bool)should.Invoke(null, ["climbingrope", 1f, 0.499f])!,
			"climbingrope placement must report after its condition cost");
		Assert.True((bool)should.Invoke(null, ["scaffoldingpack", 1f, 0.99f])!,
			"scaffoldingpack placement must report after its condition cost");
	}

	[Fact]
	public void Policy_DoesNotReportUnknownItemsOrNoOpUses()
	{
		var should = Policy.GetMethod("ShouldReport", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("DirectPlaceableArmSwingPolicy.ShouldReport not found.");

		Assert.False((bool)should.Invoke(null, ["bandage", 1f, 0.75f])!,
			"non-placeable items must not be reported");
		Assert.False((bool)should.Invoke(null, ["scrapmetal", 1f, 1f])!,
			"a gated/failed placement that did not consume condition must not be reported");
		Assert.False((bool)should.Invoke(null, ["scrapmetal", 0.5f, 0.6f])!,
			"a condition increase (not a placeable use) must not be reported");
	}

	[Fact]
	public void PatchSurface_TargetsBodyUseItemWithPrefixPostfix()
	{
		var patch = BodyItemPatches.GetNestedType("DirectPlaceableUseItemPatch", BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("BodyItemPatches.DirectPlaceableUseItemPatch not found.");

		var prefix = patch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("Prefix not found.");
		var prefixParameters = prefix.GetParameters();
		Assert.True(prefixParameters.Length == 3
			&& prefixParameters[0].Name == "__instance"
			&& prefixParameters[0].ParameterType.FullName == "Body"
			&& prefixParameters[1].Name == "item"
			&& prefixParameters[1].ParameterType.FullName == "Item"
			&& prefixParameters[2].Name == "__state"
			&& prefixParameters[2].ParameterType.IsByRef
			&& prefixParameters[2].ParameterType.GetElementType() == typeof(float),
			$"Prefix must be (Body __instance, Item item, out float __state), got {prefixParameters.Length} parameter(s)");

		var postfix = patch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("Postfix not found.");
		var postfixParameters = postfix.GetParameters();
		Assert.True(postfixParameters.Length == 3
			&& postfixParameters[0].Name == "__instance"
			&& postfixParameters[0].ParameterType.FullName == "Body"
			&& postfixParameters[1].Name == "item"
			&& postfixParameters[1].ParameterType.FullName == "Item"
			&& postfixParameters[2].Name == "__state"
			&& postfixParameters[2].ParameterType == typeof(float),
			$"Postfix must be (Body __instance, Item item, float __state), got {postfixParameters.Length} parameter(s)");
	}

	[Fact]
	public void PatchSurface_TargetsBodyUseItemInHandWithPrefixPostfix()
	{
		var patch = BodyItemPatches.GetNestedType("DirectPlaceableUseItemInHandPatch", BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("BodyItemPatches.DirectPlaceableUseItemInHandPatch not found.");

		var prefix = patch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("Prefix not found.");
		var prefixParameters = prefix.GetParameters();
		Assert.True(prefixParameters.Length == 2
			&& prefixParameters[0].Name == "__instance"
			&& prefixParameters[0].ParameterType.FullName == "Body"
			&& prefixParameters[1].Name == "__state"
			&& prefixParameters[1].ParameterType.IsByRef
			&& prefixParameters[1].ParameterType.GetElementType() == typeof(float),
			$"Prefix must be (Body __instance, out float __state), got {prefixParameters.Length} parameter(s)");

		var postfix = patch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("Postfix not found.");
		var postfixParameters = postfix.GetParameters();
		Assert.True(postfixParameters.Length == 2
			&& postfixParameters[0].Name == "__instance"
			&& postfixParameters[0].ParameterType.FullName == "Body"
			&& postfixParameters[1].Name == "__state"
			&& postfixParameters[1].ParameterType == typeof(float),
			$"Postfix must be (Body __instance, float __state), got {postfixParameters.Length} parameter(s)");
	}

	[Fact]
	public void PatchInventory_DeclaresTheBodyUseItemContract()
	{
		var inventory = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found.");
		var build = inventory.GetMethod("BuildContracts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PatchInventory.BuildContracts not found.");
		var contracts = (IEnumerable)build.Invoke(null, null)!;
		var foundUseItem = false;
		var foundUseItemInHand = false;
		foreach (var contract in contracts)
		{
			var type = contract.GetType();
			var target = type.GetProperty("TargetType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(contract) as string;
			var method = type.GetProperty("MethodName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(contract) as string;
			if (target == "Body" && method == "UseItem")
			{
				foundUseItem = true;
			}
			else if (target == "Body" && method == "UseItemInHand")
			{
				foundUseItemInHand = true;
			}
		}

		Assert.True(foundUseItem, "PatchInventory must declare the Body.UseItem patch contract (direct placeable arm swing).");
		Assert.True(foundUseItemInHand, "PatchInventory must declare the Body.UseItemInHand patch contract (direct placeable arm swing).");
	}
}
