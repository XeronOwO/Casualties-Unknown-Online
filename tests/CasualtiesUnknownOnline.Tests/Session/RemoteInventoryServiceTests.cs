using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The Online-UI remote-inventory cache: it projects the already-received
/// character-data stream (host reports, host snapshot, cross-guest relays) into
/// a read-only per-SteamID view and clears on session end. It is the "view
/// items" half of the direct player interaction backlog item; taking a remote
/// item remains a separate operation. No protocol change: the 1 Hz character
/// snapshots already carry the carried/worn item list (including recursive
/// container contents).
/// </summary>
public class RemoteInventoryServiceTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong OtherGuestId = 2002;
	private const ulong LobbyId = 9001;

	private static CharacterItemMsg Item(string itemId, int slotIndex, int contentsCount = 0) => new()
	{
		ItemId = itemId,
		SlotIndex = slotIndex,
		Condition = 50f,
		Favourited = true,
		Contents = [.. Enumerable.Range(0, contentsCount).Select(i => new CharacterItemMsg
		{
			InstanceId = (ulong)(1000 + i),
			ItemId = $"{itemId}-content-{i}",
			SlotIndex = slotIndex,
			Favourited = false,
		})],
	};

	private static CharacterItemMsg ContainerWithNested(string itemId, int slotIndex, params CharacterItemMsg[] children) => new()
	{
		ItemId = itemId,
		SlotIndex = slotIndex,
		Condition = 50f,
		Favourited = false,
		Contents = [.. children],
	};

	private static CharacterDataMsg Snapshot(ulong owner, params CharacterItemMsg[] items) => new()
	{
		OwnerSteamId = owner,
		Items = [.. items],
	};

	[Fact]
	public void Host_CachesGuestInventoryBySender()
	{
		using var host = TestNode.CreatePair(HostId, GuestId, LobbyId).Host;
		var inventory = host.Services.GetRequiredService<RemoteInventoryService>();

		host.Services.GetRequiredService<ICharacterDataControl>()
			.FireCharacterDataReceived(GuestId, Snapshot(0, Item("medkit", 0)));

		Assert.True(inventory.TryGet(GuestId, out var snapshot));
		Assert.Equal(1, snapshot.Count);
		Assert.Equal("medkit", snapshot.Items[0].ItemId);
		Assert.Equal(0, snapshot.Items[0].SlotIndex);
		Assert.True(snapshot.Items[0].Favourited);
	}

	[Fact]
	public void Guest_CachesHostInventoryByHostSteamId()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var inventory = guest.Services.GetRequiredService<RemoteInventoryService>();

			guest.Services.GetRequiredService<ICharacterDataControl>()
				.FireHostCharacterDataReceived(Snapshot(0, Item("rifle", 1)));

			Assert.True(inventory.TryGet(HostId, out var snapshot));
			Assert.Equal("rifle", snapshot.Items[0].ItemId);
			Assert.Equal(1, snapshot.Items[0].SlotIndex);
		}
	}

	[Fact]
	public void Guest_CachesCrossGuestRelayByOwnerSteamId()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var inventory = guest.Services.GetRequiredService<RemoteInventoryService>();

			// The host relays another guest's report; the transport sender is the
			// host, but the payload carries the actual owner.
			guest.Services.GetRequiredService<ICharacterDataControl>()
				.FireCharacterDataReceived(HostId, Snapshot(OtherGuestId, Item("axe", -2)));

			Assert.True(inventory.TryGet(OtherGuestId, out var snapshot));
			Assert.Equal("axe", snapshot.Items[0].ItemId);
			Assert.False(inventory.TryGet(HostId, out _));
		}
	}

	[Fact]
	public void Guest_IgnoresOwnRestoreOwnerZero()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var inventory = guest.Services.GetRequiredService<RemoteInventoryService>();

			// The host's reconnect restore of the LOCAL player arrives with
			// OwnerSteamId = 0; it is not a remote display target.
			guest.Services.GetRequiredService<ICharacterDataControl>()
				.FireCharacterDataReceived(HostId, Snapshot(0, Item("axe", 1)));

			Assert.False(inventory.TryGet(HostId, out _));
			Assert.Equal(0, inventory.Count);
		}
	}

	[Fact]
	public void RemoteLeavingWorld_ClearsThatPlayersInventory()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var inventory = guest.Services.GetRequiredService<RemoteInventoryService>();
			guest.Services.GetRequiredService<ICharacterDataControl>()
				.FireHostCharacterDataReceived(Snapshot(0, Item("axe", 1)));
			Assert.Equal(1, inventory.Count);

			((ISessionControl)guest.Session).FireRemoteSceneChanged(HostId, false);

			Assert.Equal(0, inventory.Count);
			Assert.False(inventory.TryGet(HostId, out _));
		}
	}

	[Fact]
	public void SessionEnd_ClearsTheCache()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var inventory = guest.Services.GetRequiredService<RemoteInventoryService>();
			guest.Services.GetRequiredService<ICharacterDataControl>()
				.FireHostCharacterDataReceived(Snapshot(0, Item("axe", 1)));
			Assert.Equal(1, inventory.Count);

			((ISessionControl)guest.Session).EndSession();

			Assert.Equal(0, inventory.Count);
			Assert.False(inventory.TryGet(HostId, out _));
		}
	}

	[Fact]
	public void Snapshot_ProjectsItemsAndFormats()
	{
		Assert.Null(RemoteInventorySnapshot.From(null));

		var empty = RemoteInventorySnapshot.From(Snapshot(0))!;
		Assert.Equal(0, empty.Count);
		Assert.Equal("no items", empty.ToShortString());

		var nonEmpty = RemoteInventorySnapshot.From(Snapshot(
			0,
			Item("medkit", 0),
			Item("backpack", 1, contentsCount: 2),
			Item("hat", -2)))!;
		Assert.Equal(3, nonEmpty.Count);
		Assert.Equal("3 item(s)", nonEmpty.ToShortString());
	}

	[Fact]
	public void Snapshot_ProjectsRecursiveContainerContents()
	{
		var inner = new CharacterItemMsg
		{
			InstanceId = 41,
			ItemId = "inner",
			SlotIndex = 1,
			Favourited = true,
			Contents =
			[
				new CharacterItemMsg
				{
					InstanceId = 42,
					ItemId = "deep",
					SlotIndex = 1,
				},
			],
		};

		var snapshot = RemoteInventorySnapshot.From(Snapshot(
			0,
			ContainerWithNested("backpack", 1, inner)))!;

		var backpack = Assert.Single(snapshot.Items);
		Assert.Equal(1, backpack.ContentsCount);
		var projectedInner = Assert.Single(backpack.Contents);
		Assert.Equal("inner", projectedInner.ItemId);
		Assert.True(projectedInner.Favourited);
		Assert.Equal("deep", Assert.Single(projectedInner.Contents).ItemId);
	}

	[Fact]
	public void Snapshot_ProjectsInstanceIdForTakeButtons()
	{
		var snapshot = RemoteInventorySnapshot.From(Snapshot(0, new CharacterItemMsg
		{
			InstanceId = 1234,
			ItemId = "medkit",
			SlotIndex = 0,
		}))!;

		var entry = Assert.Single(snapshot.Items);
		Assert.Equal(1234UL, entry.InstanceId);
		Assert.Equal("medkit", entry.ItemId);
	}
}
