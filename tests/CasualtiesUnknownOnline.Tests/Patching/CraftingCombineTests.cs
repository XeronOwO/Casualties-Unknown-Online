using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// 1.0.2's direct-magazine gun-load branch spends one round and leaves the
/// magazine item alive; every other gun-load branch destroys it. The report
/// must not claim a Destroyed item that is still in the inventory.
/// </summary>
[Trait("Category", "Integration")]
public class CraftingCombineTests
{
	private static readonly MethodInfo Survives = GameAssemblyHost.Adapter
		.GetType("CasualtiesUnknownOnline.GameAdapter.Items.CraftingSync", throwOnError: true)!
		.GetMethod("GunLoadAmmoSurvives", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
		?? throw new InvalidOperationException("CraftingSync.GunLoadAmmoSurvives not found.");

	[Theory]
	[InlineData(5, 4, true)]
	[InlineData(1, 0, true)]
	[InlineData(5, 5, false)]
	[InlineData(0, 0, false)]
	public void GunLoadAmmoSurvives_OnlyWhenExactlyOneRoundWasSpent(int before, int after, bool expected) =>
		Assert.Equal(expected, (bool)Survives.Invoke(null, [before, after])!);
}
