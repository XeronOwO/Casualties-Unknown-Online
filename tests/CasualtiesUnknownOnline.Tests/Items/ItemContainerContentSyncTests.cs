using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The nested-container-content flow (#120): a body-internal move inside a
/// carried container (a backpack's contents shifted) is one full parent-fact
/// report — guest → host, the host records the parent's new recursive capture
/// in the transfer table and relays it as the carried-fact event so the peers'
/// clones re-render immediately (the 1 Hz snapshot stays only as the reliable
/// fallback).
/// </summary>
public class ItemContainerContentSyncTests
{
	[Fact]
	public void GuestContainerMove_HostRecordsAndBroadcastsTheParentFact()
	{
		using var w = ItemSimWorld.Create();

		// G1 owns the backpack: host world table → transfer table.
		w.Spawn(w.G1, 101, new CharacterItemMsg { InstanceId = 101, ItemId = "backpack" });
		w.Driver.Tick(33);
		w.Pickup(w.G1, 101, new CharacterItemMsg { InstanceId = 101, ItemId = "backpack", SlotIndex = 2 });
		w.Driver.Tick(33);
		Assert.True(w.TransferredOf(w.G1, 101), "the backpack must be recorded as G1's carried item");

		var g2Carried = new List<(ulong Owner, CharacterItemMsg Item)>();
		w.G2.Services.GetRequiredService<IItemControl>().ItemCarriedSyncReceived += (owner, item, _) => g2Carried.Add((owner, item));

		// A nested-content move: a knife shifted into the backpack, the full
		// parent fact (recursive contents) is the report.
		w.G1.Services.GetRequiredService<IItemControl>().SendItemContainerContent(101,
			new CharacterItemMsg
			{
				InstanceId = 101,
				ItemId = "backpack",
				SlotIndex = 2,
				Contents =
				[
					new CharacterItemMsg { InstanceId = 202, ItemId = "knife", Condition = 0.8f },
				],
			});
		w.Driver.Tick(33);

		var fact = Assert.Single(g2Carried);
		Assert.Equal(w.G1.SteamId, fact.Owner);
		Assert.Equal(101ul, fact.Item.InstanceId);
		Assert.Equal(2, fact.Item.SlotIndex);
		var content = Assert.Single(fact.Item.Contents);
		Assert.Equal(202ul, content.InstanceId);
		Assert.Equal("knife", content.ItemId);

		// The host's transfer-table entry now carries the new recursive capture
		// (reconnect restore and future action evidence read it).
		var transferred = w.Items.GetTransferredItems(w.G1.SteamId).Single(e => e.Item.InstanceId == 101).Item;
		Assert.Contains(transferred.Contents, c => c.InstanceId == 202);

		// The owner is excluded: G1 never echoes its own report back as a fact.
		Assert.Equal(0, w.ReceivedCount(w.G1, NetMsg.ItemCarriedSync));
	}

	[Fact]
	public void GuestContainerMove_UntrackedParent_FallsBackToReportedFact()
	{
		using var w = ItemSimWorld.Create();
		var g2Carried = new List<(ulong Owner, CharacterItemMsg Item)>();
		w.G2.Services.GetRequiredService<IItemControl>().ItemCarriedSyncReceived += (owner, item, _) => g2Carried.Add((owner, item));

		// No transfer-table entry (a starting-supply/reconnect race) — the
		// guest's report is still the fact source, exactly like the use/slot
		// fallback.
		w.G1.Services.GetRequiredService<IItemControl>().SendItemContainerContent(777,
			new CharacterItemMsg
			{
				InstanceId = 777,
				ItemId = "backpack",
				SlotIndex = 1,
				Contents = [new CharacterItemMsg { InstanceId = 888, ItemId = "bandage" }],
			});
		w.Driver.Tick(33);

		var fact = Assert.Single(g2Carried);
		Assert.Equal(w.G1.SteamId, fact.Owner);
		Assert.Equal(777ul, fact.Item.InstanceId);
		Assert.Equal(1, fact.Item.SlotIndex);
		Assert.Equal(888ul, Assert.Single(fact.Item.Contents).InstanceId);
	}
}
