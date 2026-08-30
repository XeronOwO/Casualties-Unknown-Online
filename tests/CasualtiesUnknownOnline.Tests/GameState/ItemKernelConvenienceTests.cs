using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

public class ItemKernelConvenienceTests
{
	[Fact]
	public void ObserveLifecycle_DrivesKernelState()
	{
		var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
		authority.ObserveSpawn(1001, 42, "test_item", 1, 2);

		var spawned = authority.FindItem(42)!.Value;
		Assert.Equal(ItemLocationKind.World, spawned.Location.Kind);

		authority.ObservePickup(2001, 42);
		var carried = authority.FindItem(42)!.Value;
		Assert.Equal(ItemLocationKind.Carried, carried.Location.Kind);
		Assert.Equal(2001ul, carried.Location.Owner.Value);

		authority.ObserveDrop(2001, 42, 10, 20, 0);
		var dropped = authority.FindItem(42)!.Value;
		Assert.Equal(ItemLocationKind.World, dropped.Location.Kind);
		Assert.Equal(10f, dropped.Location.X);
		Assert.Equal(20f, dropped.Location.Y);

		authority.ObserveDestroy(1001, 42);
		var terminal = authority.FindItem(42)!.Value;
		Assert.Equal(ItemLocationKind.Terminal, terminal.Location.Kind);
	}

	[Fact]
	public void ResetForSession_StartsFreshEpoch()
	{
		var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
		authority.ObserveSpawn(1001, 42, "test_item", 1, 2);
		Assert.NotNull(authority.FindItem(42));

		authority.ResetForSession();

		Assert.Null(authority.FindItem(42));
	}
}
