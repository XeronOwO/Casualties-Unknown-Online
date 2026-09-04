using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Regression tests for the piggyback/carry release path. The previous fixes
/// restored the local Body's rigidbodies, but the carried-driver component is
/// destroyed with Unity's deferred <c>Object.Destroy</c>. If release does not
/// also make the driver inactive immediately, the render-proxy patches can run
/// once more in the same frame and re-freeze the just-restored body/limbs —
/// the reported "after Drop the character cannot move" symptom.
/// </summary>
public class CarriedBodyReleaseTests
{
	private static readonly Type Driver = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.CarriedBodyDriver",
		throwOnError: true)!;

	private static readonly Type Placement = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.CarriedBodyPlacement",
		throwOnError: true)!;

	private static bool IsActivelyCarried(bool driverPresent, ulong carrierSteamId)
	{
		var method = Driver.GetMethod("IsActivelyCarried", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CarriedBodyDriver.IsActivelyCarried not found.");
		return (bool)method.Invoke(null, [driverPresent, carrierSteamId])!;
	}

	[Fact]
	public void ActiveDriver_WithNonZeroCarrier_IsCarried() =>
		Assert.True(IsActivelyCarried(true, 1001UL), "a live driver with a carrier must still be a carried body.");

	[Fact]
	public void ReleasedDriver_WithZeroCarrier_IsInactiveImmediately() =>
		// This is the regression: even if Unity has not yet destroyed the
		// component (Object.Destroy is deferred to end-of-frame), the release must
		// be visible to the freeze guards immediately.
		Assert.False(IsActivelyCarried(true, 0UL), "a released but not-yet-destroyed driver must not keep the body frozen.");

	[Fact]
	public void MissingDriver_IsNotCarried() =>
		Assert.False(IsActivelyCarried(false, 1001UL));

	[Fact]
	public void ReleaseRestoreEntryPoint_ExistsOnCarriedBodyPlacement()
	{
		var method = Placement.GetMethod("RestoreLocalBody", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CarriedBodyPlacement.RestoreLocalBody not found.");
		Assert.Single(method.GetParameters());
		Assert.Equal("Body", method.GetParameters()[0].ParameterType.Name);
	}

	[Fact]
	public void RidePoseEntryPoint_IsTheSingleSharedReplacementPath()
	{
		var method = Placement.GetMethod("ApplyRidePose", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CarriedBodyPlacement.ApplyRidePose not found.");
		Assert.Equal("Body", method.GetParameters()[0].ParameterType.Name);
		Assert.True(method.GetParameters().Length >= 6, "the shared ride-pose rule must accept body, anchor, facing, crouch, velocity and look target.");
	}
}
