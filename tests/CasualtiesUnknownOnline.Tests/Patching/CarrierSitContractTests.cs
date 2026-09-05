using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Reflection contract tests for the carrier-side half of the whole-family
/// carry sit suppression. The adapter is compile-excluded (it binds game/Unity
/// assemblies), so these tests lock the shape of the patch-bridge query and the
/// remote-clone carrier flag that the pose patches depend on. The pure decision
/// logic itself is covered by <see cref="Session.CarriedBodyPoseTests"/>.
/// </summary>
public class CarrierSitContractTests
{
	private static readonly Type Bridge = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.IPatchBridge",
		throwOnError: true)!;

	private static readonly Type BridgeImpl = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.GameAdapterBridge",
		throwOnError: true)!;

	private static readonly Type Driver = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.RemoteBodyDriver",
		throwOnError: true)!;

	private static readonly Type BodyUpdatePatch = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.BodyUpdatePatch",
		throwOnError: true)!;

	[Fact]
	public void PatchBridge_HasLocalCarrierQuery()
	{
		var method = Bridge.GetMethod("IsLocalCarrier", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("IPatchBridge.IsLocalCarrier not found.");
		Assert.Equal(typeof(bool), method.ReturnType);
		var parameters = Assert.Single(method.GetParameters());
		Assert.Equal("Body", parameters.ParameterType.Name);
	}

	[Fact]
	public void GameAdapterBridge_ImplementsLocalCarrierQuery()
	{
		var method = BridgeImpl.GetMethod("IsLocalCarrier", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("GameAdapterBridge.IsLocalCarrier not found.");
		Assert.Equal(typeof(bool), method.ReturnType);
	}

	[Fact]
	public void RemoteBodyDriver_HasCarrierFlag()
	{
		var field = Driver.GetField("IsCarrier", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("RemoteBodyDriver.IsCarrier not found.");
		Assert.Equal(typeof(bool), field.FieldType);
	}

	[Fact]
	public void BodyUpdatePatch_HasLocalCarrierPostfix()
	{
		var postfix = BodyUpdatePatch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("BodyUpdatePatch.Postfix not found.");
		var parameters = Assert.Single(postfix.GetParameters());
		Assert.Equal("__instance", parameters.Name);
		Assert.Equal("Body", parameters.ParameterType.Name);
	}
}
