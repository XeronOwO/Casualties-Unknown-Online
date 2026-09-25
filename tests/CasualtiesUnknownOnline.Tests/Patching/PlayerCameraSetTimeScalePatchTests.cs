using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The world-time router's entry surface. Every <c>PlayerCamera.SetTimeScale</c>
/// call in a session is judged by ONE question: is this an announced speed
/// change (the game's own <c>switchSound</c> flag), a forced pause/death
/// transition, or a silent automatic reset? The reported case is the native
/// movement rule — <c>HandleInput</c>'s left/right check calls
/// <c>SetTimeScale(Normal, switchSound: false, force: false)</c>
/// (reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs) — which the
/// router used to treat as a speed intent, ending the session-wide acceleration
/// for everyone. These pins keep the discriminator ON the patch surface: a
/// router that cannot see <c>switchSound</c> cannot tell the two apart, and the
/// defect comes back silently. The adapter is compile-excluded, so the surface
/// is read reflectively through the shared game-assembly host.
/// </summary>
[Trait("Category", "Integration")]
public class PlayerCameraSetTimeScalePatchTests
{
	private static readonly Type Patch = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.PlayerCameraSetTimeScalePatch",
		throwOnError: true)!;

	[Fact]
	public void Prefix_SeesTheNativeSwitchSoundFlag()
	{
		var names = Parameters("Prefix");
		Assert.True(
			names.Contains("switchSound"),
			$"the SetTimeScale prefix must receive the native switchSound flag (got: {string.Join(", ", names)}); without it a silent automatic reset is indistinguishable from an announced speed change");
		Assert.True(
			names.Contains("force"),
			$"the SetTimeScale prefix must keep receiving force (got: {string.Join(", ", names)})");
	}

	[Fact]
	public void Postfix_SeesTheNativeSwitchSoundFlagAndForce()
	{
		var names = Parameters("Postfix");
		Assert.True(
			names.Contains("switchSound"),
			$"the SetTimeScale postfix must receive switchSound too — the host adopts its own applied speed from here, so a silent reset must not be reported as a speed intent (got: {string.Join(", ", names)})");
		Assert.True(
			names.Contains("force"),
			$"the SetTimeScale postfix must receive force so a paused/menu/death transition keeps its current reporting (got: {string.Join(", ", names)})");
	}

	[Fact]
	public void BridgeSeam_CarriesTheSameDiscriminators()
	{
		// The seam the patch reads must keep the flags in ITS signature too: a
		// narrowed IPatchBridge member would drop the discriminator between the
		// patch and the router, and the port-shape gate pins member NAMES only.
		var seam = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.IPatchBridge",
			throwOnError: true)!;
		AssertSeamParameters(seam, "OnTimeScaleSetRequested");
		AssertSeamParameters(seam, "OnLocalTimeScaleChanged");
	}

	private static void AssertSeamParameters(Type seam, string methodName)
	{
		var method = seam.GetMethod(methodName)
			?? throw new InvalidOperationException($"IPatchBridge.{methodName} not found.");
		var names = method.GetParameters().Select(parameter => parameter.Name!).ToArray();
		Assert.True(
			names.Contains("switchSound") && names.Contains("force"),
			$"IPatchBridge.{methodName} must carry switchSound and force (got: {string.Join(", ", names)})");
	}

	private static string[] Parameters(string methodName)
	{
		var method = Patch.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException($"PlayerCameraSetTimeScalePatch.{methodName} not found.");
		return method.GetParameters().Select(parameter => parameter.Name!).ToArray();
	}
}
