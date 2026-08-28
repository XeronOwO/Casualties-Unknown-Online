using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

public class ItemServiceTrafficTests
{
	[Fact]
	public void Reporter_RecordsSpawnAndDrop()
	{
		using var world = ItemSimWorld.Create();
		world.Spawn(world.G1, 42, Item("shell"));
		world.Drop(world.G1, 43, Item("shell"));
		world.Driver.Tick(33);

		// The reporter side records its own local-compute traffic; the host
		// receives the command envelope and no longer relays an old frame.
		var traffic = world.G1.Services.GetRequiredService<ItemService>().CurrentItemTraffic;
		Assert.Equal(2, traffic.Total);
		Assert.Equal(1, traffic.CountFor(ItemTrafficKind.Spawn));
		Assert.Equal(1, traffic.CountFor(ItemTrafficKind.Drop));
	}

	[Fact]
	public void HostSendMove_RecordsOnePerEntry()
	{
		using var world = ItemSimWorld.Create();
		world.Spawn(world.G1, 42, Item("shell"));
		world.Driver.Tick(33);

		world.Items.SendItemMove(
		[
			new WireItemMoveEntry { ItemId = 42, X = 1, Y = 2 },
		]);

		var traffic = world.Items.CurrentItemTraffic;
		Assert.Equal(1, traffic.CountFor(ItemTrafficKind.Move));
		Assert.Equal("shell", traffic.TopItems[0].ItemId);
	}

	private static CharacterItemMsg Item(string id) => new() { ItemId = id };
}
