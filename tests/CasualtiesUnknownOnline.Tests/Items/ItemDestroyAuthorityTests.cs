using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// Host authority for ItemDestroy reports. The item domain's destroy channel is
/// a WORLD-item fact: a guest may report a world item it saw destroyed, or the
/// owner of a carried item may report its own consumed/destroyed carry. A
/// destroy report from a non-owner for an item that is neither in the world
/// table nor owned by the sender must not be relayed — the remote-clone
/// inventory renderer used to destroy its display proxies with the owner's real
/// instance ids, and accepting those reports was what emptied a real owner's
/// bag from a viewer's side.
/// </summary>
public class ItemDestroyAuthorityTests
{
	private static CharacterItemMsg Item(string type = "test_item", float condition = 1f) => new()
	{
		ItemId = type,
		Condition = condition,
	};

	[Fact]
	public void NonOwnerCarriedDestroy_IsNotBroadcast()
	{
		using var w = ItemSimWorld.Create();

		// G1 picks up the item; the world-table entry transfers to G1.
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(33);
		Assert.True(w.TransferredOf(w.G1, 42), "setup: G1 owns the item");
		Assert.False(w.HostTable(42), "setup: the item is no longer a world item");

		// G2, who does not own the item, reports a destroy for it. This is
		// exactly the shape of a remote-clone display proxy reporting the
		// owner's carried id. The host must ignore it instead of relaying it.
		w.Destroy(w.G2, 42);
		w.Driver.Tick(33);

		Assert.Equal(0, w.ReceivedCount(w.G1, NetMsg.ItemDestroy));
		Assert.True(w.TransferredOf(w.G1, 42), "the non-owner destroy must not remove the owner's transfer entry");
	}

	[Fact]
	public void OwnerCarriedDestroy_RemovesTransferEntryAndBroadcasts()
	{
		using var w = ItemSimWorld.Create();

		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(33);
		Assert.True(w.TransferredOf(w.G1, 42));

		// The owner consumes/destroys its own carried item. The host should
		// remove the transfer entry (the item no longer exists to restore after
		// a reconnect) and relay the fact to the other peers.
		w.Destroy(w.G1, 42);
		w.Driver.Tick(33);

		Assert.False(w.TransferredOf(w.G1, 42), "an owner's destroy removes the transfer-table entry");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.ItemDestroy) > 0, "the other guest learns the carried item is gone");
	}
}
