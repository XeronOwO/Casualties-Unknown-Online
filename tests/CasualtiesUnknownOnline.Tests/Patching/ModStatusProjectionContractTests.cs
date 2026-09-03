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
	public void VanillaProjection_HasCirculationApplyMethods()
	{
		var type = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.ModStatus.ModStatusVanillaProjection",
			throwOnError: true)!;

		var prefix = type.GetMethod("ApplyCirculationPrefix", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("ApplyCirculationPrefix not found.");
		Assert.Single(prefix.GetParameters());
		Assert.Equal("Body", prefix.GetParameters()[0].ParameterType.Name);

		var postfix = type.GetMethod("ApplyCirculationPostfix", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("ApplyCirculationPostfix not found.");
		Assert.Single(postfix.GetParameters());
		Assert.Equal("Body", postfix.GetParameters()[0].ParameterType.Name);
	}

	[Fact]
	public void MoodleProjection_HasApplyMethod()
	{
		var type = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.ModStatus.ModStatusMoodleProjection",
			throwOnError: true)!;

		var apply = type.GetMethod("ApplyModMoodles", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("ApplyModMoodles not found.");
		Assert.Equal(2, apply.GetParameters().Length);
		Assert.Equal("MoodleManager", apply.GetParameters()[0].ParameterType.Name);
		Assert.Equal(typeof(bool), apply.GetParameters()[1].ParameterType);
	}

	[Fact]
	public void MoodlePatches_HavePrefixAndPostfix()
	{
		var patches = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Patches.ModStatusMoodlePatches",
			throwOnError: true)!;

		var nested = patches.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public);
		var patch = Assert.Single(nested, t => t.Name == "ModMoodlePatch");
		Assert.NotNull(patch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic));
		Assert.NotNull(patch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic));
	}



	[Fact]
	public void MoodleAnimationPatch_HasPostfix()
	{
		var patches = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Patches.ModStatusMoodlePatches",
			throwOnError: true)!;

		var nested = patches.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public);
		var patch = Assert.Single(nested, t => t.Name == "MoodleAnimationPatch");
		Assert.NotNull(patch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic));
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
		Assert.Contains(nested, t => t.Name == "BodyCirculationProjectionPatch"
			&& t.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic) is not null
			&& t.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic) is not null);
	}
}
