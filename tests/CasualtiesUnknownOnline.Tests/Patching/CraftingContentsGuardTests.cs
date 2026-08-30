using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Crafting-content guard decision tests: a recipe may only consume a
/// destroyable material when the item has no contents that the native destroy
/// path would drop or lose (battery, container children, liquid stack).
/// The adapter is compile-excluded, so the pure decision surface is exercised
/// reflectively.
/// </summary>
public class CraftingContentsGuardTests
{
	private static readonly Type Guard = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Items.CraftingContentsGuard",
		throwOnError: true)!;

	private static bool Refuse(bool destroyItem, bool isLiquid, bool hasBattery, bool hasContainerChildren, bool hasLiquidStack)
	{
		var method = Guard.GetMethod("ShouldRefuse", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
				null,
				[typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool)],
				null)
			?? throw new InvalidOperationException("CraftingContentsGuard.ShouldRefuse(bool...) not found.");

		return (bool)method.Invoke(null, [destroyItem, isLiquid, hasBattery, hasContainerChildren, hasLiquidStack])!;
	}

	[Theory]
	[InlineData(false, false, false, false, false)]
	[InlineData(false, true, true, false, false)]
	[InlineData(true, false, false, false, false)]
	[InlineData(true, true, true, true, true)]
	public void DestroyableWithoutContents_OrNonDestroyable_OrLiquidDrain_IsAllowed(
		bool destroyItem, bool isLiquid, bool hasBattery, bool hasContainerChildren, bool hasLiquidStack) =>
		Assert.False(Refuse(destroyItem, isLiquid, hasBattery, hasContainerChildren, hasLiquidStack));

	[Theory]
	[InlineData(true, false, true, false, false)]
	[InlineData(true, false, false, true, false)]
	[InlineData(true, false, false, false, true)]
	[InlineData(true, false, true, true, true)]
	public void DestroyableSolidWithAnyContents_IsRefused(
		bool destroyItem, bool isLiquid, bool hasBattery, bool hasContainerChildren, bool hasLiquidStack) =>
		Assert.True(Refuse(destroyItem, isLiquid, hasBattery, hasContainerChildren, hasLiquidStack));
}
