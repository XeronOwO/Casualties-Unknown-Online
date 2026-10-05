using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

public class ItemContainerSyncTests
{
	[Fact]
	public void SyncContainerFacts_CreatesContainedChildrenAsKernelItems()
	{
		var log = new RecordingLogger<ItemKernelAuthority>();
		var authority = new ItemKernelAuthority(log);
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

		Assert.True(authority.TrySyncContainerFacts(1001, withChild, out _, out var rejection), rejection?.Message ?? "rejected without message");

		var child = authority.FindItem(101)!.Value;
		Assert.Equal(ItemLocationKind.Contained, child.Location.Kind);
		Assert.Equal(100ul, child.Location.ParentItemId);
		Assert.Equal(0.5f, child.Data.Condition);
		Assert.DoesNotContain(log.Entries, entry => entry.Message.Contains("[ContainerSync]"));
	}

	[Fact]
	public void SyncContainerFacts_DestroysStaleChildren()
	{
		var log = new RecordingLogger<ItemKernelAuthority>();
		var authority = new ItemKernelAuthority(log);
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
		Assert.True(authority.TrySyncContainerFacts(1001, withChild, out _, out _));
		Assert.NotNull(authority.FindItem(101));

		Assert.True(authority.TrySyncContainerFacts(1001, new CharacterItemMsg { InstanceId = 100, ItemId = "bag" }, out _, out _));

		var child = authority.FindItem(101)!.Value;
		Assert.Equal(ItemLocationKind.Terminal, child.Location.Kind);

		// The destroy is a one-way door (a Terminal child rejects the whole container
		// sync from then on), so the branch says what it dropped instead of running
		// silently: this is the line a session reads when a stale contained record
		// turns a live child into a corpse.
		var warning = Assert.Single(log.Entries, entry => entry.Level == LogLevel.Warning);
		Assert.Contains("the report for 100 no longer names 1 item(s)", warning.Message);
		Assert.Contains("101", warning.Message);
	}
}
