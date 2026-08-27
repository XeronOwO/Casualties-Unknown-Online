using CasualtiesUnknownOnline.Runtime.GameAdapter;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// L0 reflection contract for the native remote-backpack surface: the Runtime
/// boundary exposes the open/close methods and the GameAdapter implements the
/// interface, so a UI action can rely on the adapter to open the game's native
/// radial inventory focused on a remote render clone.
/// </summary>
public class RemoteBackpackContractTests
{
	[Fact]
	public void IGameAdapter_ExposesNativeBackpackSurface()
	{
		var open = typeof(IGameAdapter).GetMethod("OpenRemoteBackpack");
		Assert.NotNull(open);
		Assert.Equal(typeof(bool), open!.ReturnType);
		var openParameters = open.GetParameters();
		Assert.Equal(2, openParameters.Length);
		Assert.Equal(typeof(ulong), openParameters[0].ParameterType);
		Assert.Equal(typeof(string), openParameters[1].ParameterType);

		var close = typeof(IGameAdapter).GetMethod("CloseRemoteBackpack");
		Assert.NotNull(close);
		Assert.Equal(typeof(void), close!.ReturnType);
	}

	[Fact]
	public void GameAdapter_ImplementsNativeBackpackSurface()
	{
		var adapter = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.GameAdapter",
			throwOnError: true)!;
		Assert.True(typeof(IGameAdapter).IsAssignableFrom(adapter));
	}

	[Fact]
	public void PatchBridge_ExposesRemoteBackpackTakeSurface()
	{
		var bridge = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.IPatchBridge",
			throwOnError: true)!;
		var take = bridge.GetMethod("TryHandleRemoteBackpackTake");
		Assert.NotNull(take);
		Assert.Equal(typeof(bool), take!.ReturnType);
		var parameter = Assert.Single(take.GetParameters());
		Assert.Equal("Item", parameter.ParameterType.Name);

		var cancel = bridge.GetMethod("CancelRemoteProxyDrag");
		Assert.NotNull(cancel);
		Assert.Equal(typeof(bool), cancel!.ReturnType);
		var cancelParameters = cancel.GetParameters();
		Assert.Equal(2, cancelParameters.Length);
		Assert.Equal("PlayerCamera", cancelParameters[0].ParameterType.Name);
		Assert.Equal(typeof(string), cancelParameters[1].ParameterType);
	}
}
