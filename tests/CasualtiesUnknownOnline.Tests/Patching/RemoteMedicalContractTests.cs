using System;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// L0 reflection contract for the native remote medical (WoundView) surface: the
/// Runtime boundary's remote-medical presentation port exposes the open method,
/// the GameAdapter implements that port, and the adapter-side static view keeps
/// the read-only focus state. The view closes itself when the target goes stale
/// (<c>RemoteMedicalCoordinator.Close</c> from its Update), so the port carries
/// no close entry point.
/// </summary>
[Trait("Category", "Integration")]
public class RemoteMedicalContractTests
{
	[Fact]
	public void IRemoteMedicalPresentation_ExposesNativeMedicalSurface()
	{
		var open = typeof(IRemoteMedicalPresentation).GetMethod("OpenRemoteMedical");
		Assert.NotNull(open);
		Assert.Equal(typeof(bool), open!.ReturnType);
		var openParameters = open.GetParameters();
		Assert.Equal(2, openParameters.Length);
		Assert.Equal(typeof(ulong), openParameters[0].ParameterType);
		Assert.Equal(typeof(string), openParameters[1].ParameterType);
	}

	[Fact]
	public void GameAdapter_ImplementsNativeMedicalSurface()
	{
		var adapter = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.GameAdapter",
			throwOnError: true)!;
		Assert.True(typeof(IRemoteMedicalPresentation).IsAssignableFrom(adapter));
	}

	[Fact]
	public void MedicalBridge_ExposesLimbUseRouting()
	{
		var bridge = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.GameAdapterBridge",
			throwOnError: true)!;

		var method = bridge.GetMethod(
			"TryHandleRemoteMedicalLimbUse",
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var parameters = method!.GetParameters();
		Assert.Equal(2, parameters.Length);
		Assert.Equal("Item", parameters[0].ParameterType.Name);
		Assert.Equal(typeof(int), parameters[1].ParameterType);
		Assert.Equal(typeof(bool), method.ReturnType);
	}

	[Fact]
	public void RemoteMedicalView_ExposesOpenCloseState()
	{
		var view = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Character.RemoteMedicalView",
			throwOnError: true)!;

		var open = view.GetMethod("Open", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.NotNull(open);
		var openParameters = open!.GetParameters();
		Assert.Equal(3, openParameters.Length);
		Assert.Equal("Body", openParameters[0].ParameterType.Name);
		Assert.Equal(typeof(ulong), openParameters[1].ParameterType);
		Assert.Equal(typeof(string), openParameters[2].ParameterType);

		var close = view.GetMethod("Close", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.NotNull(close);

		var isOpen = view.GetProperty("IsOpen", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.NotNull(isOpen);
		Assert.Equal(typeof(bool), isOpen!.PropertyType);
	}

	[Fact]
	public void TalkTextPatch_TargetsTheNativeSpeechMethod()
	{
		var patch = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Patches.RemoteMedicalPatches+RemoteMedicalTalkTextPatch",
			throwOnError: true)!;

		var attribute = Assert.Single(patch.GetCustomAttributesData(), a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch");
		Assert.Equal(2, attribute.ConstructorArguments.Count);
		Assert.Equal("PlayerCamera", (attribute.ConstructorArguments[0].Value as Type)?.Name);
		Assert.Equal("HandleOnscreenCharacterTalkText", attribute.ConstructorArguments[1].Value as string);

		var postfix = patch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.NotNull(postfix);
		var parameters = postfix!.GetParameters();
		Assert.Single(parameters);
		Assert.Equal("PlayerCamera", parameters[0].ParameterType.Name);
	}

	[Fact]
	public void TalkTextState_DecidesTheLocalRestoreOnceAfterARemoteTakeover()
	{
		var state = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Patches.RemoteMedicalPatches+RemoteMedicalTalkTextState",
			throwOnError: true)!;
		var shouldRestore = state.GetMethod(
			"ShouldRestoreLocalLine",
			BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.NotNull(shouldRestore);

		// The static is process-wide: clear any take-over a prior test left behind.
		_ = shouldRestore!.Invoke(null, [false]);
		Assert.False((bool)shouldRestore.Invoke(null, [false])!);
		Assert.False((bool)shouldRestore.Invoke(null, [true])!);
		Assert.True((bool)shouldRestore.Invoke(null, [false])!);
		Assert.False((bool)shouldRestore.Invoke(null, [false])!);
	}
}
