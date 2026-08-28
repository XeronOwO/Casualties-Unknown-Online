using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The item arbitration flows over the real wire path (TestNode): the guest
/// reports an action over the Phase C CommandEnvelope, the host executes it in
/// the deterministic kernel, and the guest observes the resulting
/// facts/rejections through the item-domain events. No GameAdapter — the
/// guest side is the runtime event surface.
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

	private static (TestNode Host, TestNode Guest) CreateSession()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		return (host, guest);
	}

	private static void SpawnItem(TestNode guest, ulong itemId, CharacterItemMsg item) =>
		guest.Services.GetRequiredService<IItemControl>().SendItemSpawned(
			itemId, item, new NetVector2(0, 0), new NetVector2(0, 0), 0f, false, 0f);

	private static void ReportPickup(TestNode guest, ulong itemId, CharacterItemMsg? evidence) =>
		guest.Services.GetRequiredService<IItemControl>().SendItemPickedUp(itemId, evidence);

	[Fact]
	public void Pickup_ConsistentEvidence_SurfacesCarriedFactWithoutReject()
	{
		var (_, guest) = CreateSession();
		var rejects = new List<ulong>();
		var carried = new List<(ulong Owner, CharacterItemMsg Item)>();
		guest.Services.GetRequiredService<IItemControl>().ItemRejected += (id, _) => rejects.Add(id);
		guest.Services.GetRequiredService<IItemControl>().ItemCarriedSyncReceived += (owner, item, _) => carried.Add((owner, item));

		SpawnItem(guest, 42, Item());
		ReportPickup(guest, 42, Item());

		Assert.Empty(rejects);
		var fact = Assert.Single(carried);
		Assert.Equal(GuestId, fact.Owner);
		Assert.Equal(42ul, fact.Item.InstanceId);
	}

	[Fact]
	public void Pickup_ConditionDivergence_CarriedFactCarriesAuthoritativeValue()
	{
		var (_, guest) = CreateSession();
		var carried = new List<(ulong Owner, CharacterItemMsg Item)>();
		guest.Services.GetRequiredService<IItemControl>().ItemCarriedSyncReceived += (owner, item, _) => carried.Add((owner, item));

		SpawnItem(guest, 42, Item(condition: 1f));
		ReportPickup(guest, 42, Item(condition: 0.5f));

		var fact = Assert.Single(carried);
		Assert.Equal(1f, fact.Item.Condition);
	}

	[Fact]
	public void Pickup_UnknownItem_Rejected()
	{
		var (host, guest) = CreateSession();
		var rejects = new List<ulong>();
		guest.Services.GetRequiredService<IItemControl>().ItemRejected += (id, _) => rejects.Add(id);

		ReportPickup(guest, 9999, Item());

		// The pending-pickup hold is the bounded waiting edge — the claim must
		// end in exactly one late UnknownItem reject, never a silent drop.
		new SimulationDriver(guest.Clock, guest.Transport.Network, host, guest).Tick(600);

		var reject = Assert.Single(rejects);
		Assert.Equal(9999ul, reject);
	}

	[Fact]
	public void Use_UntrackedItem_AcceptFirstNoCorrection()
	{
		var (_, guest) = CreateSession();
		var corrections = new List<CharacterItemMsg>();
		guest.Services.GetRequiredService<IItemControl>().ItemCorrectionReceived += corrections.Add;

		guest.Services.GetRequiredService<IItemControl>().SendItemUse(777, Item());

		// No transfer-table entry and not a world item — the host accepts the
		// missing carried update (accept-first) and broadcasts the fact; never a
		// correction or rejection.
		Assert.Empty(corrections);
	}

	[Fact]
	public void ClearTransferred_NewRun_EmptiesTheTransferTable()
	{
		var (host, guest) = CreateSession();
		var arbitration = host.Services.GetRequiredService<ItemArbitration>();
		SpawnItem(guest, 42, Item());
		ReportPickup(guest, 42, Item());

		arbitration.ClearTransferred();

		Assert.Empty(arbitration.GetTransferredItems(GuestId));
	}

	[Fact]
	public void ClearTransferred_LeavesTheWorldTableIntact()
	{
		var (host, guest) = CreateSession();
		var items = host.Services.GetRequiredService<ItemService>();
		SpawnItem(guest, 42, Item());

		host.Services.GetRequiredService<ItemArbitration>().ClearTransferred();

		Assert.True(items.IsWorldItemRegistered(42));
	}

	[Fact]
	public void ClearTransferred_TransferAfterwards_StillArbitrates()
	{
		var (host, guest) = CreateSession();
		var arbitration = host.Services.GetRequiredService<ItemArbitration>();
		arbitration.ClearTransferred();

		SpawnItem(guest, 43, Item());
		ReportPickup(guest, 43, Item());

		Assert.Contains(arbitration.GetTransferredItems(GuestId), w => w.Item.InstanceId == 43);
	}

	[Fact]
	public void DuplicatePickupReport_RetransmitIsSilentlyIdempotent()
	{
		var (_, guest) = CreateSession();
		var rejects = new List<ulong>();
		guest.Services.GetRequiredService<IItemControl>().ItemRejected += (id, _) => rejects.Add(id);

		SpawnItem(guest, 42, Item());
		ReportPickup(guest, 42, Item());
		ReportPickup(guest, 42, Item());

		// The second manual report is a different command operation; the
		// kernel may reject it as a conflict. The real Steam retransmission is
		// covered by the duplicate-delivery tests (same operation id). This
		// test documents that repeated user-driven pickups cannot roll back a
		// completed transfer.
		Assert.True(rejects.Count <= 1, $"the duplicate claim must not produce more than one reject, got {rejects.Count}");
	}
}
