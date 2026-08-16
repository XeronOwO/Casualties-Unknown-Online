using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The arbitration verdicts over the real wire path (TestNode): the guest
/// reports an action with evidence → the host's ItemService/ItemArbitration
/// decides → the guest observes the corrective traffic. No GameAdapter — the
/// guest side is the wire surface.
/// </summary>
public class ItemArbitrationFlowTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static CharacterItemMsg Item(float condition = 1f, List<CharacterItemMsg>? contents = null) => new()
	{
		ItemId = "test_item",
		Condition = condition,
		Contents = contents ?? [],
	};

	private static (TestNode Host, TestNode Guest, List<(NetMsg Msg, byte[] Frame)> Received) CreateSession()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var received = new List<(NetMsg Msg, byte[] Frame)>();
		guest.Transport.MessageReceived += (_, frame) => received.Add(((NetMsg)frame[0], frame));
		return (host, guest, received);
	}

	private static void SpawnItem(TestNode guest, ulong itemId, CharacterItemMsg item)
	{
		var sender = guest.Services.GetRequiredService<PacketSender>();
		sender.Send(HostId, NetMsg.ItemSpawn, new ItemSpawnMsg { ItemId = itemId, Item = item });
	}

	private static void ReportPickup(TestNode guest, ulong itemId, CharacterItemMsg? evidence)
	{
		var sender = guest.Services.GetRequiredService<PacketSender>();
		sender.Send(HostId, NetMsg.ItemPickup, new ItemPickupMsg { ItemId = itemId, Item = evidence });
	}

	[Fact]
	public void Pickup_ConsistentEvidence_NoCorrectionOrDestroy()
	{
		var (_, guest, received) = CreateSession();
		SpawnItem(guest, 42, Item());
		ReportPickup(guest, 42, Item());

		Assert.DoesNotContain(received, r => r.Msg is NetMsg.ItemCorrection or NetMsg.ItemDestroy);
	}

	[Fact]
	public void Pickup_ConditionDivergence_CorrectionCarriesAuthoritativeValue()
	{
		var (_, guest, received) = CreateSession();
		SpawnItem(guest, 42, Item(condition: 1f));
		ReportPickup(guest, 42, Item(condition: 0.5f));

		var frame = received.Single(r => r.Msg == NetMsg.ItemCorrection).Frame;
		var correction = NetPacket.DecodePayload<ItemCorrectionMsg>(frame);
		Assert.Equal(1f, correction.Item.Condition);
	}

	[Fact]
	public void Pickup_ExtraContentClaimed_DestroyedNotCorrected()
	{
		var (_, guest, received) = CreateSession();
		SpawnItem(guest, 42, Item());
		ReportPickup(guest, 42, Item(contents: [new CharacterItemMsg { InstanceId = 999 }]));

		var frame = received.Single(r => r.Msg == NetMsg.ItemDestroy).Frame;
		var destroy = NetPacket.DecodePayload<ItemDestroyMsg>(frame);
		Assert.Equal(999UL, destroy.ItemId);
		Assert.DoesNotContain(received, r => r.Msg == NetMsg.ItemCorrection);
	}

	[Fact]
	public void Pickup_MissingContent_CorrectionMaterializesIt()
	{
		var (_, guest, received) = CreateSession();
		SpawnItem(guest, 42, Item(contents: [new CharacterItemMsg { InstanceId = 7 }]));
		ReportPickup(guest, 42, Item());

		var frame = received.Single(r => r.Msg == NetMsg.ItemCorrection).Frame;
		var correction = NetPacket.DecodePayload<ItemCorrectionMsg>(frame);
		Assert.Contains(correction.Item.Contents, c => c.InstanceId == 7);
	}

	[Fact]
	public void Pickup_UnknownItem_Rejected()
	{
		var (host, guest, received) = CreateSession();
		ReportPickup(guest, 9999, Item());

		// The pending-pickup hold is the bounded waiting edge — the claim must
		// end in exactly one late UnknownItem reject, never a silent drop.
		new SimulationDriver(guest.Clock, guest.Transport.Network, host, guest).Tick(600);

		var frame = received.Single(r => r.Msg == NetMsg.ItemReject).Frame;
		var reject = NetPacket.DecodePayload<ItemRejectMsg>(frame);
		Assert.Equal(9999UL, reject.ItemId);
	}

	[Fact]
	public void Use_UntrackedItem_NoCorrection()
	{
		var (_, guest, received) = CreateSession();
		var sender = guest.Services.GetRequiredService<PacketSender>();
		sender.Send(HostId, NetMsg.ItemUse, new ItemUseMsg { ItemId = 777, Item = Item() });

		// No transfer-table entry and not a world item — the guest's report is the fact source, never corrected.
		Assert.DoesNotContain(received, r => r.Msg == NetMsg.ItemCorrection);
	}

	[Fact]
	public void ClearTransferred_NewRun_EmptiesTheTransferTable()
	{
		var (host, guest, _) = CreateSession();
		var arbitration = host.Services.GetRequiredService<ItemArbitration>();
		SpawnItem(guest, 42, Item());
		ReportPickup(guest, 42, Item()); // the pickup transfers the entry

		arbitration.ClearTransferred();

		Assert.Empty(arbitration.GetTransferredItems(GuestId));
	}

	[Fact]
	public void ClearTransferred_LeavesTheWorldTableIntact()
	{
		var (host, guest, _) = CreateSession();
		var items = host.Services.GetRequiredService<ItemService>();
		SpawnItem(guest, 42, Item());

		host.Services.GetRequiredService<ItemArbitration>().ClearTransferred();

		// A NEW run clears only the carried records — the world items of the old
		// world are the OLD world's, they leave with its scene (ItemService.ResetItems
		// resets the table at generation). The transfer table holds inventory facts,
		// the world table holds world facts — the clear must not conflate them.
		Assert.True(items.IsWorldItemRegistered(42));
	}

	[Fact]
	public void ClearTransferred_TransferAfterwards_StillArbitrates()
	{
		var (host, guest, received) = CreateSession();
		var arbitration = host.Services.GetRequiredService<ItemArbitration>();
		arbitration.ClearTransferred();

		SpawnItem(guest, 43, Item());
		ReportPickup(guest, 43, Item());

		// The new run's first pickup transfers normally — the clear is a reset,
		// not a shutdown.
		Assert.Contains(arbitration.GetTransferredItems(GuestId), w => w.Item.InstanceId == 43);
		Assert.DoesNotContain(received, r => r.Msg == NetMsg.ItemReject);
	}

	[Fact]
	public void DuplicatePickupReport_RetransmitIsSilentlyIdempotent()
	{
		var (_, guest, received) = CreateSession();
		SpawnItem(guest, 42, Item());
		ReportPickup(guest, 42, Item()); // the entry transfers to the guest

		received.Clear();
		ReportPickup(guest, 42, Item()); // a retransmit would re-report the same pickup

		// The item is no longer in the world table, but the sender ALREADY owns
		// it (the transfer table) — a rejection would roll the winner's own
		// successful pickup back, so the duplicate is silent. The one-shot
		// operation cannot double-execute either way (the world-table check).
		Assert.DoesNotContain(received, r => r.Msg == NetMsg.ItemReject);
	}
}
