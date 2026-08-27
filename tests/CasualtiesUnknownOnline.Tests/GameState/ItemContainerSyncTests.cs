using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

public class ItemContainerSyncTests
{
	[Fact]
	public void SyncContainerContents_CreatesContainedChildrenAsKernelItems()
	{
		var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
		var parent = new CharacterItemMsg { InstanceId = 100, ItemId = "bag", Condition = 1f };
		authority.TrySpawn(1001, new ItemIdentity(100, "bag"), ItemLocation.World(1, 2), parent, out _, out _);

		var withChild = new CharacterItemMsg
		{
			InstanceId = 100,
			ItemId = "bag",
			Contents =
			[
				new CharacterItemMsg { InstanceId = 101, ItemId = "water", Condition = 0.5f },
			],
		};

		authority.SyncContainerContents(1001, 100, withChild, new ActorId(1001));

		var child = authority.FindItem(101)!.Value;
		Assert.Equal(ItemLocationKind.Contained, child.Location.Kind);
		Assert.Equal(100ul, child.Location.ParentItemId);
		Assert.Equal(0.5f, child.Data.Condition);
	}

	[Fact]
	public void SyncContainerContents_DestroysStaleChildren()
	{
		var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
		var parent = new CharacterItemMsg { InstanceId = 100, ItemId = "bag", Condition = 1f };
		authority.TrySpawn(1001, new ItemIdentity(100, "bag"), ItemLocation.World(1, 2), parent, out _, out _);

		var withChild = new CharacterItemMsg
		{
			InstanceId = 100,
			ItemId = "bag",
			Contents =
			[
				new CharacterItemMsg { InstanceId = 101, ItemId = "water", Condition = 0.5f },
			],
		};
		authority.SyncContainerContents(1001, 100, withChild, new ActorId(1001));
		Assert.NotNull(authority.FindItem(101));

		authority.SyncContainerContents(1001, 100, new CharacterItemMsg { InstanceId = 100, ItemId = "bag" }, new ActorId(1001));

		var child = authority.FindItem(101)!.Value;
		Assert.Equal(ItemLocationKind.Terminal, child.Location.Kind);
	}
}
