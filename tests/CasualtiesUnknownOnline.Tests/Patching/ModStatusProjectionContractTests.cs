using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Reflective contract for the phase-3 GameAdapter status projection seam. The
/// test project cannot compile-reference GameAdapter (it binds game
/// assemblies), so these assertions lock the type names/method shapes the
/// Harmony postfixes and the bridge rely on.
/// </summary>
public class ModStatusProjectionContractTests
{
	[Fact]
	public void VanillaProjection_HasBodyAndLimbApplyMethods()
	{
		var type = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.ModStatus.ModStatusVanillaProjection",
			throwOnError: true)!;

		var applyBody = type.GetMethod("ApplyBody", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("ApplyBody not found.");
		Assert.Single(applyBody.GetParameters());
		Assert.Equal("Body", applyBody.GetParameters()[0].ParameterType.Name);

		var applyLimb = type.GetMethod("ApplyLimb", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("ApplyLimb not found.");
		Assert.Equal(2, applyLimb.GetParameters().Length);
		Assert.Equal("Body", applyLimb.GetParameters()[0].ParameterType.Name);
		Assert.Equal("Limb", applyLimb.GetParameters()[1].ParameterType.Name);
	}

	[Fact]
	public void ProjectionPatches_HaveBodyAndLimbPostfixes()
	{
		var patches = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Patches.ModStatusProjectionPatches",
			throwOnError: true)!;

		var nested = patches.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public);
		Assert.Contains(nested, t => t.Name == "BodyStatusProjectionPatch"
			&& t.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic) is not null);
		Assert.Contains(nested, t => t.Name == "LimbStatusProjectionPatch"
			&& t.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic) is not null);
	}
}
