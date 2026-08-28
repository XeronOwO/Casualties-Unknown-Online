using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The item-action flows over the three-node simulated world: a used WORLD
/// item (drinking from a ground canister, #194) adopts its state into the
/// host's world table and corrects every side — the craft domain's WorldChange
/// philosophy; a used CARRIED item keeps the transfer-table adoption + carried
/// fact broadcast. The host's own world-item use broadcasts the correction
/// through the same send surface (SendWorldItemCorrection — the adapter's
/// ItemUseSync host branch calls it).
/// </summary>
public class ItemActionSyncTests
{
	private static CharacterItemMsg Item(float condition = 1f, List<LiquidStackMsg>? liquids = null) => new()
	{
		ItemId = "test_item",
		Condition = condition,
		Liquids = liquids ?? [],
	};

	[Fact]
	public void GuestUsesWorldItem_HostAdoptsStateAndCorrectsEverySide()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, Item(liquids: [new LiquidStackMsg { LiquidId = "water", Amount = 1f }]));
		var hostCorrections = new List<CharacterItemMsg>();
		w.Host.Services.GetRequiredService<IItemControl>().ItemCorrectionReceived += hostCorrections.Add;
		var g2Corrections = new List<CharacterItemMsg>();
		w.G2.Services.GetRequiredService<IItemControl>().ItemCorrectionReceived += g2Corrections.Add;

		var used = Item(condition: 0.6f, liquids: [new LiquidStackMsg { LiquidId = "water", Amount = 0.4f }]);
		w.Use(w.G1, 42, used);
		w.Driver.Tick(33);

		// The host's world table adopted the used state and its own scene copy
		// corrected (the FireCorrectionLocal event carries the adopted state).
		var hostCorrection = Assert.Single(hostCorrections);
		Assert.Equal(42ul, hostCorrection.InstanceId); // the world branch stamps the id — the receivers locate the world copy by it
		Assert.Equal(0.4f, hostCorrection.Liquids.Single(l => l.LiquidId == "water").Amount);

		// Every other member's projection re-surfaces the authoritative state as
		// the world-correction event (Phase C batch projection).
		Assert.Single(g2Corrections);
		Assert.True(w.HostTable(42), "the world item stays in the table (its state changed, it was not consumed)");
	}

	[Fact]
	public void GuestUsesCarriedItem_StillAdoptsAndBroadcastsTheCarriedFact()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, Item());
		w.Pickup(w.G1, 42, Item());
		var g2Carried = new List<(ulong Owner, CharacterItemMsg Item)>();
		w.G2.Services.GetRequiredService<IItemControl>().ItemCarriedSyncReceived += (owner, item, _) => g2Carried.Add((owner, item));
		w.Driver.Tick(33); // the pickup's own carried-fact broadcast arrives

		var before = g2Carried.Count;
		w.Use(w.G1, 42, Item(condition: 0.5f));
		w.Driver.Tick(33);

		// The carried path is unchanged: the batch projection re-surfaces as the
		// carried-fact event, never a world correction.
		Assert.Equal(before + 1, g2Carried.Count);
		Assert.Equal(0.5f, w.Items.GetTransferredItems(w.G1.SteamId).Single(e => e.Item.InstanceId == 42).Item.Condition);
	}

	[Fact]
	public void HostWorldItemUse_CorrectsBothGuests()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, Item(condition: 1f));
		w.Driver.Tick(33);

		var g1Corrections = new List<CharacterItemMsg>();
		var g2Corrections = new List<CharacterItemMsg>();
		w.G1.Services.GetRequiredService<IItemControl>().ItemCorrectionReceived += g1Corrections.Add;
		w.G2.Services.GetRequiredService<IItemControl>().ItemCorrectionReceived += g2Corrections.Add;

		w.Host.Services.GetRequiredService<IItemControl>().SendWorldItemCorrection(
			w.Host.SteamId, new CharacterItemMsg { InstanceId = 42, ItemId = "test_item", Condition = 0.3f });
		w.Driver.Tick(33);

		// The adapter's host-side use branch (ItemUseSync.OnItemUsed) calls this
		// surface: the committed batch projection re-surfaces on every guest.
		Assert.Single(g1Corrections);
		Assert.Single(g2Corrections);
	}

	[Fact]
	public void UntrackedUse_NotAWorldItem_KeepsTheFallbackBroadcast()
	{
		using var w = ItemSimWorld.Create();
		var g2Carried = new List<(ulong Owner, CharacterItemMsg Item)>();
		w.G2.Services.GetRequiredService<IItemControl>().ItemCarriedSyncReceived += (owner, item, _) => g2Carried.Add((owner, item));

		w.Use(w.G1, 777, Item());
		w.Driver.Tick(33);

		// No transfer-table entry and not a world item — the accept-first carried
		// spawn re-surfaces as the carried-fact event, never corrected.
		Assert.Single(g2Carried);
	}
}
