using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Contract and behavior tests for the local-carrier carry mount surface. The
/// repeated rider-teleport reports were traced to the rider clone being pinned
/// to the carrier while still being an independent scene root: any transform
/// movement that Unity applies after LateUpdate (Rigidbody render
/// interpolation, final script ordering) could still separate the pair. The fix
/// re-parents a local carrier's rider clone under a neutral-scale mount, so the
/// mount scale math and the attach/detach surface must be correct and stable.
/// The same pass also reads whether a clone's exact limb poses travelled with
/// the root it just wrote, so that read-only surface is pinned here too.
/// </summary>
[Trait("Category", "Integration")]
public class CarriedRiderMountTests
{
	private static readonly Type Renderer = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.RemotePlayerRenderer",
		throwOnError: true)!;

	private static readonly Type Placement = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.CarriedBodyPlacement",
		throwOnError: true)!;

	private static readonly Type Vector3Type =
		Type.GetType("UnityEngine.Vector3, UnityEngine.CoreModule")
		?? throw new InvalidOperationException("UnityEngine.Vector3 type not found.");

	private static readonly MethodInfo CarryMountScale = Placement.GetMethod(
		"CarryMountScale",
		BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
		?? throw new InvalidOperationException("CarriedBodyPlacement.CarryMountScale not found.");

	[Fact]
	public void LocalCarrierMountSurface_HasCreateAttachAndDetach()
	{
		var getMount = Renderer.GetMethod("GetOrCreateCarryMount", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("RemotePlayerRenderer.GetOrCreateCarryMount not found.");
		var attach = Renderer.GetMethod("AttachCarriedRiderRoot", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("RemotePlayerRenderer.AttachCarriedRiderRoot not found.");
		var detach = Renderer.GetMethod("DetachCarriedRiderRoot", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("RemotePlayerRenderer.DetachCarriedRiderRoot not found.");

		Assert.True(getMount.IsStatic);
		Assert.True(attach.IsStatic);
		Assert.True(detach.IsStatic);

		Assert.Equal("Transform", getMount.GetParameters()[0].ParameterType.Name);
		Assert.Equal("Transform", getMount.ReturnType.Name);
		Assert.Equal("Body", attach.GetParameters()[0].ParameterType.Name);
		Assert.Equal("Transform", attach.GetParameters()[1].ParameterType.Name);
		Assert.Equal("Body", detach.GetParameters()[0].ParameterType.Name);
	}

	[Theory]
	[InlineData(1f, 1f, 1f, 1f, 1f, 1f)]
	[InlineData(-1f, 1f, 1f, -1f, 1f, 1f)]
	[InlineData(2f, 1f, 1f, 0.5f, 1f, 1f)]
	[InlineData(-2f, 1f, 1f, -0.5f, 1f, 1f)]
	[InlineData(0f, 1f, 1f, 1f, 1f, 1f)]
	public void CarryMountScale_NeutralizesCarrierWorldScale(
		float carrierX, float carrierY, float carrierZ,
		float expectedX, float expectedY, float expectedZ)
	{
		var result = (object)CarryMountScale.Invoke(null, [NewVector3(carrierX, carrierY, carrierZ)])!;
		Assert.True(Math.Abs(GetVectorComponent(result, "x") - expectedX) < 0.0001f);
		Assert.True(Math.Abs(GetVectorComponent(result, "y") - expectedY) < 0.0001f);
		Assert.True(Math.Abs(GetVectorComponent(result, "z") - expectedZ) < 0.0001f);
	}

	[Fact]
	public void CarriedLimbMeasurementSurface_IsWiredIntoTheRidePose()
	{
		// The ride pose is the one frame point where the clone's root has just
		// been written, so it owns the read-only limb check and the per-clone
		// reference shape that check reads: both must exist on the built adapter
		// with this shape.
		var application = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Character.RagdollPoseApplication",
			throwOnError: true)!;
		var measure = application.GetMethod("MeasurePinnedRootSeparation", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("RagdollPoseApplication.MeasurePinnedRootSeparation not found.");
		Assert.True(measure.IsStatic);
		Assert.Equal("Void", measure.ReturnType.Name);
		Assert.Equal("Body", measure.GetParameters()[0].ParameterType.Name);

		var driver = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Character.RemoteBodyDriver",
			throwOnError: true)!;
		var anchor = driver.GetField("LimbAnchor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("RemoteBodyDriver.LimbAnchor not found.");
		Assert.Equal("CarriedLimbAnchor", anchor.FieldType.Name);
		var window = driver.GetField("LimbSeparationWindowMax", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("RemoteBodyDriver.LimbSeparationWindowMax not found.");
		Assert.Equal("Single", window.FieldType.Name);
	}

	private static object NewVector3(float x, float y, float z) =>
		Activator.CreateInstance(Vector3Type, x, y, z)!;

	private static float GetVectorComponent(object vector, string component)
	{
		var field = Vector3Type.GetField(component)
			?? throw new InvalidOperationException($"UnityEngine.Vector3.{component} not found.");
		return (float)field.GetValue(vector)!;
	}
}
