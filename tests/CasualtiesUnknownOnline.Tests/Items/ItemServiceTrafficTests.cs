using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

public class ItemServiceTrafficTests
{
	[Fact]
	public void HostRelay_RecordsSpawnAndDrop()
	{
		using var world = ItemSimWorld.Create();
		world.Spawn(world.G1, 42, Item("shell"));
		world.Drop(world.G1, 43, Item("shell"));
		world.Driver.Tick(33);

		var traffic = world.Items.CurrentItemTraffic;
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
			new ItemMoveEntryMsg { ItemId = 42, X = 1, Y = 2 },
		]);

		var traffic = world.Items.CurrentItemTraffic;
		Assert.Equal(1, traffic.CountFor(ItemTrafficKind.Move));
		Assert.Equal("shell", traffic.TopItems[0].ItemId);
	}

	private static CharacterItemMsg Item(string id) => new() { ItemId = id };
}
