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
}
