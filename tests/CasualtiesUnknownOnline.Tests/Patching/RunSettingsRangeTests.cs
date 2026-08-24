using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The pure co-op run-settings range policy. This is a host-side UI-range
/// decision that must not change the wire path: only the slider upper bounds
/// are adjusted, and the selected values continue to ride the existing
/// world-start params. The policy is exercised reflectively because the
/// GameAdapter is compile-excluded from the test project (it binds game
/// assemblies).
/// </summary>
public class RunSettingsRangeTests
{
	private static readonly Type Policy = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Run.RunSettingsRange",
		throwOnError: true)!;

	private static (float Min, float Max) ForCoOp(string name, float min, float max, int memberCount)
	{
		var method = Policy.GetMethod("ForCoOp", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("RunSettingsRange.ForCoOp not found.");
		var result = (ValueTuple<float, float>)method.Invoke(null, [name, min, max, memberCount])!;
		return (result.Item1, result.Item2);
	}

	[Fact]
	public void ScalableSetting_SoloKeepsOriginalRange()
	{
		var (min, max) = ForCoOp("baselootdensity", 0f, 2f, 1);

		Assert.Equal(0f, min);
		Assert.Equal(2f, max);
	}

	[Fact]
	public void ScalableSetting_TwoPlayersDoublesTheUpperBound()
	{
		var (min, max) = ForCoOp("baselootdensity", 0f, 2f, 2);

		Assert.Equal(0f, min);
		Assert.Equal(4f, max);
	}

	[Fact]
	public void ScalableSetting_ThreePlayersTriplesTheUpperBound()
	{
		var (min, max) = ForCoOp("timelimit", 5f, 300f, 3);

		Assert.Equal(5f, min);
		Assert.Equal(900f, max);
	}

	[Fact]
	public void PercentageSetting_KeepsItsSemanticCap()
	{
		var (min, max) = ForCoOp("traderchance", 0f, 100f, 4);

		Assert.Equal(0f, min);
		Assert.Equal(100f, max);
	}

	[Fact]
	public void OffsetSetting_KeepsItsOriginalRange()
	{
		var (min, max) = ForCoOp("temperatureoffset", -80f, 50f, 4);

		Assert.Equal(-80f, min);
		Assert.Equal(50f, max);
	}

	[Fact]
	public void UnknownSetting_KeepsItsOriginalRange()
	{
		var (min, max) = ForCoOp("totally-new-setting", 1f, 10f, 4);

		Assert.Equal(1f, min);
		Assert.Equal(10f, max);
	}

	[Fact]
	public void NonScalableSetting_IsNotReportedAsScalable()
	{
		var isScalable = Policy.GetMethod("IsScalable", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("RunSettingsRange.IsScalable not found.");
		Assert.False((bool)isScalable.Invoke(null, ["traderchance"])!);
		Assert.True((bool)isScalable.Invoke(null, ["baselootdensity"])!);
	}
}
