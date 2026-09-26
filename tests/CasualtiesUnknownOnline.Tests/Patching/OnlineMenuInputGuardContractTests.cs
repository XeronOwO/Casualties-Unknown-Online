using System.Reflection;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// L0 reflection contract for the Online UI's native input blocker: the adapter exposes the port the
/// plugin drives, and the modal and ESC facts stay shaped the way the plugin calls them.
///
/// <para>
/// The scoped rectangle API this file used to pin is gone (ticket online-ui-art-and-controls-overhaul, S5):
/// the quick panel and the in-world player context menu are controls of CUO's own uGUI surface now, so they
/// block their own pixels and the port no longer carries a rectangle-list member — the shape gate that
/// counts the port's members (<see cref="AdapterCapabilityPortShapeTests"/>) is what holds that.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class OnlineMenuInputGuardContractTests
{
	[Fact]
	public void GameAdapter_ImplementsTheInputBlockerPort()
	{
		var adapter = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.GameAdapter",
			throwOnError: true)!;
		Assert.True(typeof(INativeInputBlocker).IsAssignableFrom(adapter));
	}

	[Fact]
	public void INativeInputBlocker_ExposesModalSetter()
	{
		var method = typeof(INativeInputBlocker).GetMethod("SetOnlineUiModal");
		Assert.NotNull(method);
		Assert.Equal(typeof(void), method!.ReturnType);
		var parameter = Assert.Single(method.GetParameters());
		Assert.Equal(typeof(bool), parameter.ParameterType);
	}

	[Fact]
	public void INativeInputBlocker_ExposesEscapeSurfaceSetter()
	{
		var method = typeof(INativeInputBlocker).GetMethod("SetOnlineUiEscapeSurfaceVisible");
		Assert.NotNull(method);
		Assert.Equal(typeof(void), method!.ReturnType);
		var parameter = Assert.Single(method.GetParameters());
		Assert.Equal(typeof(bool), parameter.ParameterType);
	}

	[Fact]
	public void OnlineMenuInputGuard_HasEscapeSurfaceSetter()
	{
		var guard = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.OnlineMenuInputGuard",
			throwOnError: true)!;
		var setter = guard.GetMethod("SetNonModalEscapeSurfaceVisible", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.NotNull(setter);
		var parameter = Assert.Single(setter!.GetParameters());
		Assert.Equal(typeof(bool), parameter.ParameterType);
	}

	[Fact]
	public void OnlineMenuInputGuard_HasNoScopedRectangleSetter()
	{
		// The negative half of the same retirement: a rectangle-list member regrown on the guard would
		// mean the surface's own panels are being blocked from outside again.
		var guard = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.OnlineMenuInputGuard",
			throwOnError: true)!;
		Assert.Null(guard.GetMethod("SetScopedBlocks", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
	}
}
