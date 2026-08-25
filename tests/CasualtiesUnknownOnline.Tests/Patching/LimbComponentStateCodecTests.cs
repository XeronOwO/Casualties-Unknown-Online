using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Reflective surface for the Game Adapter's limb component capture/apply
/// helper. The dynamic limb components (SplintLimb/TourniquetScript/ChilledLimb)
/// live on limb GameObjects, not on Body/Limb, so they cannot ride Mapster;
/// this locks the helper shape that CharacterDataSync calls from the 1 Hz
/// snapshot and cross-player item-use results.
/// </summary>
public sealed class LimbComponentStateCodecTests
{
	private static readonly Type Codec = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.LimbComponentStateCodec",
		throwOnError: true)!;

	[Fact]
	public void Surface_HasCaptureAndApplyForLimbComponentState()
	{
		var capture = Codec.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("LimbComponentStateCodec.Capture not found.");
		var apply = Codec.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("LimbComponentStateCodec.Apply not found.");

		var captureParameter = Assert.Single(capture.GetParameters());
		Assert.Equal("Limb", captureParameter.ParameterType.Name);

		var applyParameter = apply.GetParameters()[1];
		Assert.Equal("Limb", apply.GetParameters()[0].ParameterType.Name);
		Assert.Equal("List`1", applyParameter.ParameterType.Name);
		Assert.Equal("CasualtiesUnknownOnline.Runtime.Protocol.Messages.ComponentStateMsg", applyParameter.ParameterType.GetGenericArguments()[0].FullName);
	}

	[Fact]
	public void CaptureAndApply_AreStaticMethods()
	{
		var capture = Codec.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("LimbComponentStateCodec.Capture not found.");
		var apply = Codec.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("LimbComponentStateCodec.Apply not found.");

		Assert.True(capture.IsStatic, "Capture must be a static method (the helper owns no state).");
		Assert.True(apply.IsStatic, "Apply must be a static method (the helper owns no state).");
	}
}
