using System.Collections.Generic;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// L0 reflection contract for the scoped anti-passthrough surface: the adapter
/// must expose the non-modal Online UI blocker API, and the raycast filter type
/// that makes the full-canvas transparent blocker only intercept inside CUO
/// panel rectangles must stay on the adapter assembly.
/// </summary>
public class OnlineMenuInputGuardContractTests
{
	[Fact]
	public void IGameAdapter_ExposesScopedBlockSurface()
	{
		var method = typeof(IGameAdapter).GetMethod("SetOnlineUiScopedBlocks");
		Assert.NotNull(method);
		Assert.Equal(typeof(void), method!.ReturnType);
		var parameter = Assert.Single(method.GetParameters());
		Assert.Equal(typeof(IReadOnlyList<OnlineUiBlockRect>), parameter.ParameterType);
	}

	[Fact]
	public void GameAdapter_ImplementsScopedBlockSurface()
	{
		var adapter = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.GameAdapter",
			throwOnError: true)!;
		Assert.True(typeof(IGameAdapter).IsAssignableFrom(adapter));
	}

	[Fact]
	public void OnlineMenuInputGuard_HasScopedBlockSetter()
	{
		var guard = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.OnlineMenuInputGuard",
			throwOnError: true)!;
		var setter = guard.GetMethod("SetScopedBlocks", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.NotNull(setter);
		var parameter = Assert.Single(setter!.GetParameters());
		Assert.Equal(typeof(IReadOnlyList<OnlineUiBlockRect>), parameter.ParameterType);
	}

	[Fact]
	public void OnlineScopedRaycastFilter_ImplementsCanvasRaycastFilter()
	{
		var filter = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.OnlineScopedRaycastFilter",
			throwOnError: true)!;
		Assert.Contains(
			filter.GetInterfaces(),
			type => type.FullName == "UnityEngine.ICanvasRaycastFilter");

		var valid = filter.GetMethod(
			"IsRaycastLocationValid",
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		Assert.NotNull(valid);
		Assert.Equal(typeof(bool), valid!.ReturnType);
	}
}
