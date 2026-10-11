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
///
/// <para>
/// The apply names its MODE at every call site — the authoritative half (the
/// body that owns the limb, where an omitted component is a removal) and the
/// display half (a body CUO only renders, where the instances must stay inert)
/// are different operations, and a single unnamed entry point let a caller pick
/// one by accident. The two methods therefore have to keep the same signature
/// and stay static, and no unnamed <c>Apply</c> may come back.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class LimbComponentStateCodecTests
{
	private static readonly Type Codec = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.LimbComponentStateCodec",
		throwOnError: true)!;

	[Fact]
	public void Surface_HasCaptureAndBothApplyModesForLimbComponentState()
	{
		var capture = Codec.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("LimbComponentStateCodec.Capture not found.");

		var captureParameter = Assert.Single(capture.GetParameters());
		Assert.Equal("Limb", captureParameter.ParameterType.Name);

		foreach (var name in new[] { "ApplyAuthoritative", "ApplyDisplay" })
		{
			var apply = Codec.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
				?? throw new InvalidOperationException($"LimbComponentStateCodec.{name} not found.");

			Assert.True(apply.IsStatic, $"{name} must be a static method (the helper owns no state).");
			Assert.Equal("Limb", apply.GetParameters()[0].ParameterType.Name);
			Assert.Equal("List`1", apply.GetParameters()[1].ParameterType.Name);
			Assert.Equal("CasualtiesUnknownOnline.Runtime.Protocol.Messages.ComponentStateMsg", apply.GetParameters()[1].ParameterType.GetGenericArguments()[0].FullName);
		}

		// One unnamed entry point is exactly what made the two halves interchangeable in a caller's mind.
		Assert.Null(Codec.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
	}
}
