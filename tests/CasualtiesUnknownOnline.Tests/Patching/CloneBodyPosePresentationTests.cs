using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Reflective surface for the remote-clone body-pose presentation helper. The
/// adapter is compile-excluded from the test project, so the capture/apply
/// shape and the clone driver field are locked here the same way as the other
/// adapter contract tests.
/// </summary>
public class CloneBodyPosePresentationTests
{
	private static readonly Type Presentation = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.CloneBodyPosePresentation",
		throwOnError: true)!;

	private static readonly Type Driver = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.RemoteBodyDriver",
		throwOnError: true)!;

	[Fact]
	public void Surface_HasStaticCaptureAndApply()
	{
		var capture = Presentation.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CloneBodyPosePresentation.Capture not found.");
		var apply = Presentation.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CloneBodyPosePresentation.Apply not found.");

		Assert.True(capture.IsStatic);
		Assert.True(apply.IsStatic);

		var captureParameters = capture.GetParameters();
		Assert.Equal(2, captureParameters.Length);
		Assert.Equal("Body", captureParameters[0].ParameterType.Name);
		Assert.Equal("CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterHealthMsg", captureParameters[1].ParameterType.FullName);

		var applyParameters = apply.GetParameters();
		Assert.Equal(2, applyParameters.Length);
		Assert.Equal("Body", applyParameters[0].ParameterType.Name);
		Assert.Equal("CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterHealthMsg", applyParameters[1].ParameterType.FullName);
	}

	[Fact]
	public void RemoteBodyDriver_HasLegSpeedMultField()
	{
		var field = Driver.GetField("LegSpeedMult", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("RemoteBodyDriver.LegSpeedMult not found.");
		Assert.Equal(typeof(float), field.FieldType);
	}
}
