using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Regression tests for the piggyback/carry release facing bug. While a local
/// body is carried, CUO rewrites <c>Body.isRight</c> to match the carrier's
/// facing, but does not rewrite the transform scale sign that actually renders
/// the sprite; after release the body can be left with isRight and scale out of
/// agreement, so the native auto-flip path can no longer turn the visual.
/// The shared facing rule must keep the two in lockstep on every write.
/// </summary>
public class BodyFacingTests
{
	private static readonly Type Facing = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.BodyFacing",
		throwOnError: true)!;

	[Theory]
	[InlineData(true, 1f, 1f)]
	[InlineData(false, 1f, -1f)]
	[InlineData(true, -2.5f, 2.5f)]
	[InlineData(false, -2.5f, -2.5f)]
	public void FacingScale_MirrorsLogicalFacingIntoScaleSign(bool isRight, float currentScaleX, float expectedScaleX)
	{
		var method = Facing.GetMethod("FacingScale", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("BodyFacing.FacingScale not found.");
		var actual = (float)method.Invoke(null, [isRight, currentScaleX])!;
		Assert.True(Math.Abs(actual - expectedScaleX) < 0.001f,
			$"BodyFacing.FacingScale({isRight}, {currentScaleX}) = {actual}, expected {expectedScaleX}.");
	}

	[Fact]
	public void Apply_TakesALocalBodyAndReconcilesScale()
	{
		var method = Facing.GetMethod("Apply", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("BodyFacing.Apply not found.");
		var parameters = method.GetParameters();
		Assert.Single(parameters);
		Assert.Equal("Body", parameters[0].ParameterType.Name);
	}
}
