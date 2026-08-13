using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The crafting domain's message flow over the three-node simulated world:
/// ONE craft report carries the operation's complete terminal state; the host
/// classifies per entry against its tables (world vs transfer), applies,
/// stamps the relay routing and relays the WHOLE report (source excluded —
/// never decomposed into per-entry broadcasts). Accept-with-adopt, never
/// reject: the sender's consumption is irreversible.
/// </summary>
public class CraftSyncSimulationTests
{
	private static CraftEntryMsg Destroyed(ulong id) =>
		new() { Disposition = CraftEntryDisposition.Destroyed, Item = new CharacterItemMsg { InstanceId = id, ItemId = "cloth" } };

	private static CraftEntryMsg Changed(ulong id, float condition) =>
		new() { Disposition = CraftEntryDisposition.Changed, Item = new CharacterItemMsg { InstanceId = id, ItemId = "knife", Condition = condition } };

	private static CharacterItemMsg Product(ulong id, string type = "bandage") =>
		new() { InstanceId = id, ItemId = type, Condition = 1f, SlotIndex = 3 };

	private static CharacterItemMsg WorldItem(ulong id, float condition = 1f) =>
		new() { InstanceId = id, ItemId = "cloth", Condition = condition };

	[Fact]
	public void GuestCraft_FloorMaterialDestroyed_WorldTableRemovedAndRelayed()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, WorldItem(42));

		w.Craft(w.G1, new CraftReportMsg { Kind = CraftOperationKind.Craft, Entries = [Destroyed(42)], Products = [Product(44)] });
		w.Driver.Tick(33);

		Assert.False(w.HostTable(42), "the consumed floor material leaves the host's world table");
		Assert.True(w.TransferredOf(w.G1, 44), "the product registers in the crafter's transfer table");
		Assert.Equal(0, w.ReceivedCount(w.G1, NetMsg.CraftReport)); // the source is excluded from the relay
		Assert.Equal(1, w.ReceivedCount(w.G2, NetMsg.CraftReport)); // the WHOLE report relays once — never per-entry broadcasts
	}

	[Fact]
	public void GuestCraft_CarriedMaterialDestroyed_TransferTableRemoved()
	{
		// A carried material's destruction must leave the transfer table — the
		// ghost would otherwise resurrect the item on reconnect (the restore
		// merge replays the transfer entries).
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, WorldItem(42));
		w.Pickup(w.G1, 42, WorldItem(42));

		Assert.True(w.TransferredOf(w.G1, 42));
		w.Craft(w.G1, new CraftReportMsg { Entries = [Destroyed(42)] });
		w.Driver.Tick(33);

		Assert.False(w.TransferredOf(w.G1, 42), "the destroyed carried material leaves the transfer table");
	}

	[Fact]
	public void GuestCraft_ChangedEntry_AdoptsConditionIntoTransferTable()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, WorldItem(42));
		w.Pickup(w.G1, 42, WorldItem(42));

		w.Craft(w.G1, new CraftReportMsg { Entries = [Changed(42, 0.4f)] });
		w.Driver.Tick(33);

		var entry = w.Items.GetTransferredItems(w.G1.SteamId).Single(e => e.Item.InstanceId == 42);
		Assert.Equal(0.4f, entry.Item.Condition); // the protobuf float round-trip preserves the exact bits
	}

	[Fact]
	public void GuestCraft_Products_RegisteredInTransferTable()
	{
		using var w = ItemSimWorld.Create();

		w.Craft(w.G1, new CraftReportMsg { Products = [Product(44), Product(45, "gun")] });
		w.Driver.Tick(33);

		Assert.True(w.TransferredOf(w.G1, 44));
		Assert.True(w.TransferredOf(w.G1, 45));
	}

	[Fact]
	public void GuestCraft_WorldChanged_FiresCorrectionWithTheReducedCondition()
	{
		// A destroyItem=false floor material (knife, cloth — 99 recipes use
		// them) leaves the world with a reduced condition: the host's table and
		// scene copy must adopt it, and the correction event is the scene
		// channel (ItemApplication.OnItemCorrection on every side).
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, WorldItem(42));
		var corrections = new System.Collections.Generic.List<CharacterItemMsg>();
		w.Host.Services.GetRequiredService<IItemControl>().ItemCorrectionReceived += corrections.Add;

		w.Craft(w.G1, new CraftReportMsg { Entries = [Changed(42, 0.6f)] });
		w.Driver.Tick(33);

		var correction = Assert.Single(corrections);
		Assert.Equal(42ul, correction.InstanceId);
		Assert.Equal(0.6f, correction.Condition);
		Assert.True(w.HostTable(42), "the world item stays in the table (its condition changed, it was not consumed)");
	}

	[Fact]
	public void GuestCraft_DuplicateReport_IsIdempotent()
	{
		// A Steam-reliable retransmit re-applies the same report: every apply
		// step is idempotent (table removes no-op, registration overwrites) —
		// no reject, no crash, the state lands once.
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, WorldItem(42));
		w.Pickup(w.G1, 42, WorldItem(42));

		w.Craft(w.G1, new CraftReportMsg { Entries = [Destroyed(42)], Products = [Product(44)] });
		w.Craft(w.G1, new CraftReportMsg { Entries = [Destroyed(42)], Products = [Product(44)] });
		w.Driver.Tick(33);

		Assert.False(w.TransferredOf(w.G1, 42));
		Assert.True(w.TransferredOf(w.G1, 44));
		Assert.Empty(w.Rejects(w.G1));
	}

	[Fact]
	public void GuestCraft_UnknownEntries_SkippedNoReject()
	{
		// Never rejected (the consumption is irreversible on the sender — a
		// race with another guest's pickup): the untracked entry skips with a
		// warning, and the known entries still apply.
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, WorldItem(42));

		w.Craft(w.G1, new CraftReportMsg { Entries = [Destroyed(42), Destroyed(999)], Products = [Product(44)] });
		w.Driver.Tick(33);

		Assert.False(w.HostTable(42));
		Assert.True(w.TransferredOf(w.G1, 44));
		Assert.Empty(w.Rejects(w.G1));
	}

	[Fact]
	public void GuestCraft_RaceWithOtherGuestsPickup_CraftFirst_PickupRefused()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, WorldItem(42));

		w.Craft(w.G1, new CraftReportMsg { Entries = [Destroyed(42)] });
		w.Pickup(w.G2, 42, WorldItem(42)); // the losing pickup after the craft consumed the item
		w.Driver.Tick(33);

		Assert.False(w.HostTable(42));
		var reject = Assert.Single(w.Rejects(w.G2));
		Assert.Equal(42ul, reject.ItemId);
	}

	[Fact]
	public void GuestCraft_RaceWithOtherGuestsPickup_PickupFirst_CraftEntrySkipped()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, WorldItem(42));
		w.Pickup(w.G2, 42, WorldItem(42)); // G2 wins the item first

		w.Craft(w.G1, new CraftReportMsg { Entries = [Destroyed(42)] });
		w.Driver.Tick(33);

		Assert.True(w.TransferredOf(w.G2, 42), "the winner's transfer entry survives — the craft's unknown entry skips");
		Assert.Empty(w.Rejects(w.G1));
	}

	[Fact]
	public void HostCraft_BroadcastsToBothGuests_AndAppliesItsOwnWorldTable()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, WorldItem(42));

		w.Host.Services.GetRequiredService<ICraftControl>().ReportCraft(
			new CraftReportMsg { Entries = [Destroyed(42)], Products = [Product(44)] });
		w.Driver.Tick(33);

		Assert.False(w.HostTable(42), "the host's own craft removes the consumed floor material from its own table");
		Assert.Equal(1, w.ReceivedCount(w.G1, NetMsg.CraftReport));
		Assert.Equal(1, w.ReceivedCount(w.G2, NetMsg.CraftReport));
	}

	[Fact]
	public void RecipeUnlock_GuestReport_RelaysExcludingSource_EventEverywhereElse()
	{
		using var w = ItemSimWorld.Create();
		var hostUnlocks = 0;
		var g2Unlocks = 0;
		w.Host.Services.GetRequiredService<ICraftControl>().RecipeUnlockReceived += _ => hostUnlocks++;
		w.G2.Services.GetRequiredService<ICraftControl>().RecipeUnlockReceived += _ => g2Unlocks++;

		w.Unlock(w.G1, 3);
		w.Driver.Tick(33);

		Assert.Equal(1, hostUnlocks);
		Assert.Equal(1, g2Unlocks);
		Assert.Equal(0, w.ReceivedCount(w.G1, NetMsg.RecipeUnlock)); // the source is excluded — its own static was set by the game's useAction
		Assert.Equal(1, w.ReceivedCount(w.G2, NetMsg.RecipeUnlock));
	}

	[Fact]
	public void RecipeUnlock_HostOwn_BroadcastsToBothGuests()
	{
		using var w = ItemSimWorld.Create();
		var g1Unlocks = 0;
		var g2Unlocks = 0;
		w.G1.Services.GetRequiredService<ICraftControl>().RecipeUnlockReceived += _ => g1Unlocks++;
		w.G2.Services.GetRequiredService<ICraftControl>().RecipeUnlockReceived += _ => g2Unlocks++;

		w.Host.Services.GetRequiredService<ICraftControl>().SendRecipeUnlock(3);
		w.Driver.Tick(33);

		Assert.Equal(1, g1Unlocks);
		Assert.Equal(1, g2Unlocks);
		Assert.Equal(1, w.ReceivedCount(w.G1, NetMsg.RecipeUnlock));
		Assert.Equal(1, w.ReceivedCount(w.G2, NetMsg.RecipeUnlock));
	}
}
