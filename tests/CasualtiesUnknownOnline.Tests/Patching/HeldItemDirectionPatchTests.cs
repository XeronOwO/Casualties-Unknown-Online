using System;
using System.Linq;
using System.Reflection;
using Xunit;
using System.Collections;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The #119 patch surface and its angle rule. The adapter is compile-excluded
/// from the test project (it binds game/Unity assemblies), so the pure angle
/// helper and the patch shape are exercised reflectively — the same host as
/// the other contract tests.
/// </summary>
public class HeldItemDirectionPatchTests
{
	private static readonly Type Direction = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.HeldItemDirection",
		throwOnError: true)!;

	private static readonly Type Patch = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.HeldItemDirectionPatch",
		throwOnError: true)!;

	private static float AngleFor(float itemX, float itemY, float lookX, float lookY, float offsetDegrees)
	{
		var method = Direction.GetMethod("AngleFor", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("HeldItemDirection.AngleFor not found.");
		return (float)method.Invoke(null, [itemX, itemY, lookX, lookY, offsetDegrees])!;
	}

	[Fact]
	public void AimRight_HasZeroAngle()
	{
		var angle = AngleFor(0f, 0f, 10f, 0f, 0f);
		Assert.True(Math.Abs(angle - 0f) < 0.001f, $"aiming right must be 0 degrees, got {angle}");
	}

	[Fact]
	public void AimUp_HasNinetyDegrees()
	{
		var angle = AngleFor(0f, 0f, 0f, 10f, 0f);
		Assert.True(Math.Abs(angle - 90f) < 0.001f, $"aiming up must be 90 degrees, got {angle}");
	}

	[Fact]
	public void FlashlightAndEmergencyLight_SubtractNinetyDegrees()
	{
		var angle = AngleFor(0f, 0f, 0f, 10f, -90f);
		Assert.True(Math.Abs(angle - 0f) < 0.001f, $"flashlight aiming up must be 0 degrees, got {angle}");
	}

	[Fact]
	public void ZeroLengthAim_ReturnsTheItemKindOffset()
	{
		var angle = AngleFor(3f, 4f, 3f, 4f, -90f);
		Assert.True(Math.Abs(angle - -90f) < 0.001f, $"zero-length aim must keep the item-kind offset, got {angle}");
	}

	[Fact]
	public void PatchSurface_TargetsCustomItemBehaviourUpdateWithPostfix()
	{
		var postfix = Patch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("HeldItemDirectionPatch.Postfix not found.");
		var parameters = postfix.GetParameters();
		Assert.True(parameters.Length == 1 && parameters[0].Name == "__instance"
			&& parameters[0].ParameterType.FullName == "CustomItemBehaviour",
			$"Postfix must have exactly one __instance of CustomItemBehaviour, got {parameters.Length} parameter(s)");
	}

	[Fact]
	public void PatchInventory_ContainsTheHeldItemDirectionContract()
	{
		var inventory = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found.");
		var build = inventory.GetMethod("BuildContracts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PatchInventory.BuildContracts not found.");
		var contracts = (IEnumerable)build.Invoke(null, null)!;
		var found = contracts.Cast<object>().Any(c =>
		{
			var type = c.GetType();
			var target = type.GetProperty("TargetType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string;
			var method = type.GetProperty("MethodName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string;
			return target == "CustomItemBehaviour" && method == "Update";
		});

		Assert.True(found, "PatchInventory must declare the CustomItemBehaviour.Update patch contract (#119).");
	}
}
