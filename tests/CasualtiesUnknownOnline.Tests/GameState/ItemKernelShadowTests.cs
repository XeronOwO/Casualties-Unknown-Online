using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

public class ItemKernelShadowTests
{
	[Fact]
	public void ObserveLifecycle_DrivesKernelState()
	{
		var shadow = new ItemKernelShadow(NullLogger<ItemKernelShadow>.Instance);
		shadow.ObserveSpawn(1001, 42, "test_item", 1, 2);

		var spawned = shadow.KernelForDiagnostics.FindItem(42)!.Value;
		Assert.Equal(ItemLocationKind.World, spawned.Location.Kind);

		shadow.ObservePickup(2001, 42);
		var carried = shadow.KernelForDiagnostics.FindItem(42)!.Value;
		Assert.Equal(ItemLocationKind.Carried, carried.Location.Kind);
		Assert.Equal(2001ul, carried.Location.Owner.Value);

		shadow.ObserveDrop(2001, 42, 10, 20, 0);
		var dropped = shadow.KernelForDiagnostics.FindItem(42)!.Value;
		Assert.Equal(ItemLocationKind.World, dropped.Location.Kind);
		Assert.Equal(10f, dropped.Location.X);
		Assert.Equal(20f, dropped.Location.Y);

		shadow.ObserveDestroy(1001, 42);
		var terminal = shadow.KernelForDiagnostics.FindItem(42)!.Value;
		Assert.Equal(ItemLocationKind.Terminal, terminal.Location.Kind);
	}

	[Fact]
	public void ResetForSession_StartsFreshEpoch()
	{
		var shadow = new ItemKernelShadow(NullLogger<ItemKernelShadow>.Instance);
		shadow.ObserveSpawn(1001, 42, "test_item", 1, 2);
		Assert.NotNull(shadow.KernelForDiagnostics.FindItem(42));

		shadow.ResetForSession();

		Assert.Null(shadow.KernelForDiagnostics.FindItem(42));
	}
}
