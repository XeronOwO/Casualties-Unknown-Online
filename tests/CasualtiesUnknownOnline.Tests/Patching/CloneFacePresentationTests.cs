using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Reflective surface for the unified remote character display projection.
/// The adapter is compile-excluded from the test project, so the unified
/// capture/apply surface is locked here the same way as the other adapter
/// contract tests. The old per-domain helpers (CloneFacePresentation,
/// CloneBodyPosePresentation, RemoteMedicalDisplayProjection) have been
/// absorbed into this single seam.
/// </summary>
public class CloneFacePresentationTests
{
	private static readonly Type Projection = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.RemoteCharacterDisplayProjection",
		throwOnError: true)!;

	[Fact]
	public void Surface_HasCaptureAndApplyForBodyFaceLatches()
	{
		var capture = Projection.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("RemoteCharacterDisplayProjection.Capture not found.");
		var apply = Projection.GetMethod("ApplyRenderClone", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("RemoteCharacterDisplayProjection.ApplyRenderClone not found.");

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
		var capture = Projection.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("RemoteCharacterDisplayProjection.Capture not found.");
		var apply = Projection.GetMethod("ApplyRenderClone", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("RemoteCharacterDisplayProjection.ApplyRenderClone not found.");

		Assert.True(capture.IsStatic, "Capture must be a static method (the projection owns no state).");
		Assert.True(apply.IsStatic, "ApplyRenderClone must be a static method (the projection owns no state).");
	}

	[Fact]
	public void Apply_UsesPureFaceVitalsProjection()
	{
		var applyVitals = Projection.GetMethod("ApplyFaceVitals", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("RemoteCharacterDisplayProjection.ApplyFaceVitals not found.");

		Assert.True(applyVitals.IsStatic, "ApplyFaceVitals must be a static method (the helper owns no state).");
		var parameters = applyVitals.GetParameters();
		Assert.Equal(2, parameters.Length);
		Assert.Equal("Body", parameters[0].ParameterType.Name);
		Assert.Equal("CasualtiesUnknownOnline.Runtime.Session.CharacterData.FacePresentationVitals", parameters[1].ParameterType.FullName);
	}

	[Fact]
	public void Surface_HasMedicalDisplayApplyAndAdvance()
	{
		Assert.NotNull(Projection.GetMethod("ApplyMedicalDisplay", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
		Assert.NotNull(Projection.GetMethod("AdvanceMedicalDisplay", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
	}
}
