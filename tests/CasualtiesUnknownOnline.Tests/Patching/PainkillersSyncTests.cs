using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Reflective surface for the Game Adapter's painkiller component capture/apply
/// helper. The component state lives on <c>Painkillers</c>, not on <c>Body</c>,
/// so it cannot ride Mapster; this contract locks the helper shape that
/// <c>CharacterDataSync</c> calls from the 1 Hz snapshot and host results.
/// </summary>
public sealed class PainkillersSyncTests
{
	private static readonly Type Sync = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.PainkillersSync",
		throwOnError: true)!;

	[Fact]
	public void Surface_HasCaptureAndApplyForPainkillerComponent()
	{
		var capture = Sync.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PainkillersSync.Capture not found.");
		var apply = Sync.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PainkillersSync.Apply not found.");

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
		var capture = Sync.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PainkillersSync.Capture not found.");
		var apply = Sync.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PainkillersSync.Apply not found.");

		Assert.True(capture.IsStatic, "Capture must be a static method (the helper owns no state).");
		Assert.True(apply.IsStatic, "Apply must be a static method (the helper owns no state).");
	}
}
