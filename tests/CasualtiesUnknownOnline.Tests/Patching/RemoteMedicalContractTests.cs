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
}
