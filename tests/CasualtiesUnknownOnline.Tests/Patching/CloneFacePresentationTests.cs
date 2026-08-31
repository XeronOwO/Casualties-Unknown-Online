using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Reflective surface for the remote-clone FacialExpression presentation
/// helper. The adapter is compile-excluded from the test project, so the
/// capture/apply shape is locked here the same way as the other adapter
/// contract tests.
/// </summary>
public class CloneFacePresentationTests
{
	private static readonly Type Presentation = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.CloneFacePresentation",
		throwOnError: true)!;

	[Fact]
	public void Surface_HasCaptureAndApplyForBodyFaceLatches()
	{
		var capture = Presentation.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CloneFacePresentation.Capture not found.");
		var apply = Presentation.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CloneFacePresentation.Apply not found.");

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
	public void CaptureAndApply_AreStaticMethods()
	{
		var capture = Presentation.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CloneFacePresentation.Capture not found.");
		var apply = Presentation.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CloneFacePresentation.Apply not found.");

		Assert.True(capture.IsStatic, "Capture must be a static method (the helper owns no state).");
		Assert.True(apply.IsStatic, "Apply must be a static method (the helper owns no state).");
	}

	[Fact]
	public void Apply_UsesPureFaceVitalsProjection()
	{
		var applyVitals = Presentation.GetMethod("ApplyVitals", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CloneFacePresentation.ApplyVitals not found.");

		Assert.True(applyVitals.IsStatic, "ApplyVitals must be a static method (the helper owns no state).");
		var parameters = applyVitals.GetParameters();
		Assert.Equal(2, parameters.Length);
		Assert.Equal("Body", parameters[0].ParameterType.Name);
		Assert.Equal("CasualtiesUnknownOnline.Runtime.Session.CharacterData.FacePresentationVitals", parameters[1].ParameterType.FullName);
	}

}
