using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The wall-slide + landing presentation surface. The owner's
/// <c>Body.HandleGroundedState</c> plays the Wall/Grounded clips, wall-slide
/// particle/audio and the native landing dust; this test locks the render-side
/// helper shapes, the remote driver fields and the landing postfix state so a
/// game update cannot silently drop the visual.
/// </summary>
public class WallSlideLandingSyncTests
{
	private static readonly Type Driver = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.RemoteBodyDriver",
		throwOnError: true)!;

	private static readonly Type Presentation = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.WallSlidePresentation",
		throwOnError: true)!;

	private static readonly Type LandingPatch = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.BodyPatches+BodyHandleGroundedStatePatch",
		throwOnError: true)!;

	private static readonly Type LandingSync = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.CharacterLandingVisualSync",
		throwOnError: true)!;

	[Fact]
	public void RemoteDriver_HasWallSlideFlags()
	{
		var left = Driver.GetField("SlidingLeft", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("RemoteBodyDriver.SlidingLeft not found.");
		var right = Driver.GetField("SlidingRight", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("RemoteBodyDriver.SlidingRight not found.");
		Assert.Equal(typeof(bool), left.FieldType);
		Assert.Equal(typeof(bool), right.FieldType);
	}

	[Fact]
	public void WallSlidePresentation_AppliesBodySlidingFields_AndUpdatesEffects()
	{
		var apply = Presentation.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("WallSlidePresentation.Apply not found.");
		Assert.Equal(typeof(void), apply.ReturnType);
		var applyParams = apply.GetParameters();
		Assert.True(applyParams.Length == 3
			&& applyParams[0].ParameterType.FullName == "Body"
			&& applyParams[1].ParameterType == typeof(bool)
			&& applyParams[2].ParameterType == typeof(bool),
			$"Apply must be (Body, bool, bool), got {applyParams.Length} parameter(s)");

		var effects = Presentation.GetMethod("UpdateEffects", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("WallSlidePresentation.UpdateEffects not found.");
		Assert.Equal(typeof(void), effects.ReturnType);
		var effectParams = effects.GetParameters();
		Assert.True(effectParams.Length == 3
			&& effectParams[0].ParameterType.FullName == "Body"
			&& effectParams[1].ParameterType == typeof(bool)
			&& effectParams[2].ParameterType == typeof(bool),
			$"UpdateEffects must be (Body, bool, bool), got {effectParams.Length} parameter(s)");
	}

	[Fact]
	public void LandingPatch_HasPostfixWithLandingState_AndCloudSizeHelper()
	{
		var nested = LandingPatch.GetNestedType("LandingState", BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("BodyHandleGroundedStatePatch.LandingState not found.");

		var scope = nested.GetField("Scope", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("LandingState.Scope not found.");
		var isLocal = nested.GetField("IsLocalBody", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("LandingState.IsLocalBody not found.");
		var wasGrounded = nested.GetField("WasGrounded", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("LandingState.WasGrounded not found.");
		Assert.Equal(typeof(bool), isLocal.FieldType);
		Assert.Equal(typeof(bool), wasGrounded.FieldType);
		Assert.NotNull(scope.FieldType);

		var postfix = LandingPatch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("BodyHandleGroundedStatePatch.Postfix not found.");
		var parameters = postfix.GetParameters();
		Assert.True(parameters.Length == 2
			&& parameters[0].Name == "__instance"
			&& parameters[0].ParameterType.FullName == "Body"
			&& parameters[1].Name == "__state"
			&& parameters[1].ParameterType == nested,
			$"Postfix must be (Body __instance, LandingState __state), got {parameters.Length} parameter(s)");

		var helper = LandingPatch.GetMethod("LandingCloudSize", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("BodyHandleGroundedStatePatch.LandingCloudSize not found.");
		Assert.Equal(typeof(byte), helper.ReturnType);
		var helperParams = helper.GetParameters();
		Assert.True(helperParams.Length == 1 && helperParams[0].ParameterType.FullName == "Body",
			$"LandingCloudSize must be (Body), got {helperParams.Length} parameter(s)");
	}

	[Fact]
	public void LandingSync_ReportTakesCloudSizePositionVelocityX()
	{
		var report = LandingSync.GetMethod("Report", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CharacterLandingVisualSync.Report not found.");
		var parameters = report.GetParameters();
		Assert.True(parameters.Length == 3
			&& parameters[0].ParameterType == typeof(byte)
			&& parameters[1].ParameterType.FullName == "UnityEngine.Vector2"
			&& parameters[2].ParameterType == typeof(float),
			$"Report must be (byte, Vector2, float), got {parameters.Length} parameter(s)");
	}
}
