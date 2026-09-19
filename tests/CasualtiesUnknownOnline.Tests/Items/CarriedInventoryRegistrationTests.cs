using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The host side of the carried-inventory registration (sync-coverage audit row
/// I8): <c>CarriedInventory</c> is a guest's ABSOLUTE statement of the item ids
/// it self-assigned, and the host turns it into transfer-table entries. The
/// registration is therefore REPEATABLE by design — a swallowed frame is healed
/// by a later one — so the host's half must be registration-only: it learns ids
/// it does not know and never rolls back a record the host has already
/// arbitrated, and it never adopts an id the kernel places with somebody else or
/// has already terminated.
/// </summary>
[Trait("Category", "Integration")]
public class CarriedInventoryRegistrationTests
{
	private static CharacterItemMsg Item(ulong instanceId, string definitionId = "bandage", float condition = 1f) => new()
	{
		InstanceId = instanceId,
		ItemId = definitionId,
		Condition = condition,
		Contents = [],
	};

	private static void Register(TestNode guest, params CharacterItemMsg[] items) =>
		guest.Services.GetRequiredService<IItemControl>().SendCarriedInventory(items);

	private static CharacterItemMsg? Transferred(ItemSimWorld world, TestNode guest, ulong itemId)
	{
		foreach (var entry in world.Items.GetTransferredItems(guest.SteamId))
		{
			if (entry.Item.InstanceId == itemId)
			{
				return entry.Item;
			}
		}

		return null;
	}

	[Fact]
	public void RepeatedRegistration_DoesNotRollBackTheHostsArbitratedEntry()
	{
		using var w = ItemSimWorld.Create();
		Register(w.G1, Item(101));
		w.Driver.Tick(50);

		// The host's entry is the arbitrated record: a cross-player heal consumed
		// part of the item's condition (ItemArbitration.UpdateTransferredItem —
		// the reconnect restore merges this entry over the snapshot).
		w.Host.Services.GetRequiredService<ItemArbitration>().UpdateTransferredItem(
			w.G1.SteamId, 101, Item(101, condition: 0.2f));

		// The guest's re-report carries its own local copy, which has not caught
		// up yet — a registration learns ids, it does not overrule the record.
		Register(w.G1, Item(101));
		w.Driver.Tick(50);

		var entry = Transferred(w, w.G1, 101);
		Assert.True(entry is not null, "the already-registered id must stay in the transfer table");
		Assert.True(entry!.Condition == 0.2f, $"the host's arbitrated condition must survive a re-report, got {entry.Condition}");

		// The kernel is the same record: a registration states IDS, never state — the
		// guest's state channels (use/slot/container reports) are the ones that carry
		// state, so a repeat must not roll the kernel back either.
		var kernel = w.Host.Services.GetRequiredService<ItemKernelAuthority>().FindItem(101);
		Assert.True(kernel is not null, "the registration spawned the carried fact");
		Assert.True(
			ItemKernelAuthority.ToCharacterItem(kernel!.Value).Condition == 0.2f,
			"the kernel's authoritative condition must survive a re-report too");
	}

	[Fact]
	public void RegistrationOfADestroyedId_DoesNotResurrectTheEntry()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, Item(42));
		w.Pickup(w.G1, 42);
		w.Destroy(w.G1, 42);
		w.Driver.Tick(50);
		Assert.False(w.TransferredOf(w.G1, 42), "the destroy removed the item from the transfer table");

		// A stale re-report (the guest's local destroy has not reached the host, or
		// its capture predates it) must not revive a terminal fact: the kernel holds
		// item 42 as Terminal, and the reconnect restore would resurrect the ghost.
		Register(w.G1, Item(42));
		w.Driver.Tick(50);

		Assert.False(w.TransferredOf(w.G1, 42), "a terminal item id must never re-enter the transfer table");
	}

	[Fact]
	public void RegistrationOfAnotherGuestsItem_IsNotAdopted()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, Item(42));
		w.Pickup(w.G1, 42);
		w.Driver.Tick(50);
		Assert.True(w.TransferredOf(w.G1, 42), "the pickup registered the item for its owner");

		// The kernel is the carried-ownership authority; a registration for an id it
		// already places with another guest is a divergence, not a second owner.
		Register(w.G2, Item(42));
		w.Driver.Tick(50);

		Assert.False(w.TransferredOf(w.G2, 42), "another guest's carried item must not be adopted twice");
		Assert.True(w.TransferredOf(w.G1, 42), "the owner's entry must stay untouched");
	}

	[Fact]
	public void SwallowedRegistration_ConvergesOnTheDenseReReport()
	{
		using var w = ItemSimWorld.Create();
		var items = w.G1.Services.GetRequiredService<IItemControl>();

		// The guest's world generation finished: the window opens, and the lazy-P2P
		// swallow window eats every CarriedInventory frame on the way to the host.
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { DropMessageId = NetMsg.CarriedInventory });
		items.ArmCarriedInventoryRegistration("the local generation finished");
		Pump(w, w.G1, [Item(101), Item(102)], 500);
		Assert.False(w.TransferredOf(w.G1, 101), "the swallowed registration left the host without a record");

		// The window closes: the next dense re-report carries the same absolute set.
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults());
		Pump(w, w.G1, [Item(101), Item(102)], 6_000);

		Assert.True(w.TransferredOf(w.G1, 101), "the re-report must register the first starting supply");
		Assert.True(w.TransferredOf(w.G1, 102), "the re-report must register the second starting supply");
	}

	[Fact]
	public void Rejoin_ReRegistersOnce_WithoutDuplicatingTheEntry()
	{
		using var w = ItemSimWorld.Create();
		Register(w.G1, Item(101));
		w.Driver.Tick(50);
		Assert.True(w.TransferredOf(w.G1, 101), "the first registration registered the id");

		// The host's registration went missing (the reconnect restore rebuilds this
		// record) and the guest rejoined: its join edge is the watermark grant.
		w.Host.Services.GetRequiredService<ItemArbitration>().ClearTransferred();
		var items = w.G1.Services.GetRequiredService<IItemControl>();
		items.ArmCarriedInventoryRegistration("the host granted the id watermark (join/reconnect)");
		Pump(w, w.G1, [Item(101)], 500);

		Assert.True(w.TransferredOf(w.G1, 101), "the join edge re-registers the carried set");
		Assert.True(w.Items.GetTransferredItems(w.G1.SteamId).Count == 1, "the rebuild must stay exactly one entry per id");
	}

	[Fact]
	public void TwoGuests_KeepSeparateTables()
	{
		using var w = ItemSimWorld.Create();

		// The id space is (counter << 32) | SteamId: the same counter on two guests is
		// two different ids, and each guest's table stands on its own.
		var g1Id = (1UL << 32) | 2001;
		var g2Id = (1UL << 32) | 3001;
		Register(w.G1, Item(g1Id));
		Register(w.G2, Item(g2Id));
		w.Driver.Tick(50);

		Assert.True(w.TransferredOf(w.G1, g1Id), "G1 owns its allocation");
		Assert.True(w.TransferredOf(w.G2, g2Id), "G2 owns its own allocation of the same counter");
		Assert.False(w.TransferredOf(w.G1, g2Id), "one guest's registration never leaks into the other's table");
	}

	[Fact]
	public void RegistrationBeforeTheItemFact_ArbitratesTheLaterReport()
	{
		using var w = ItemSimWorld.Create();
		Register(w.G1, Item(101, condition: 1f));
		w.Driver.Tick(50);

		// The registration is the item's first fact: the guest's next report about it
		// is arbitrated against the entry instead of falling through to the no-entry
		// path, so the reported condition is adopted.
		w.Use(w.G1, 101, Item(101, condition: 0.5f));
		w.Driver.Tick(50);

		var entry = Transferred(w, w.G1, 101);
		Assert.True(entry is not null, "the registered id keeps its transfer-table entry");
		Assert.True(entry!.Condition == 0.5f, $"the use report must be arbitrated against the entry, got {entry.Condition}");
	}

	[Fact]
	public void EmptyCapture_SpendsTheWindowStep_SoThePumpDoesNotReCaptureEveryFrame()
	{
		using var w = ItemSimWorld.Create();
		var items = w.G1.Services.GetRequiredService<IItemControl>();

		items.ArmCarriedInventoryRegistration("the local generation finished");
		Assert.True(items.IsCarriedInventoryRegistrationDue(), "an opened window is due at once");

		items.SendCarriedInventory([]); // the guest carries nothing (yet)
		Assert.False(items.IsCarriedInventoryRegistrationDue(), "an empty capture still spends the step");

		w.Driver.Tick(5_000);
		Assert.True(items.IsCarriedInventoryRegistrationDue(), "the next dense step is due one interval later");
	}

	[Fact]
	public void AnIdThatAppearsAfterTheFirstReport_ConvergesOnTheNextReport()
	{
		using var w = ItemSimWorld.Create();
		var items = w.G1.Services.GetRequiredService<IItemControl>();
		var carried = new List<CharacterItemMsg> { Item(101) };

		items.ArmCarriedInventoryRegistration("the local generation finished");
		Pump(w, w.G1, carried, 500);
		Assert.True(w.TransferredOf(w.G1, 101), "the first step of the window registered the starting supply");

		// The host's record is gone (the registration frame was swallowed) and the guest has
		// since self-assigned another id (a crafted product, an item unloaded from a
		// container). The production capture states the CURRENT set, so the next report
		// carries both ids.
		w.Host.Services.GetRequiredService<ItemArbitration>().ClearTransferred();
		carried.Add(Item(202));
		Pump(w, w.G1, carried, 6_000);

		Assert.True(w.TransferredOf(w.G1, 101), "the re-report re-registered the starting supply");
		Assert.True(w.TransferredOf(w.G1, 202), "an id that appears after the first report converges on the next report");
	}

	[Fact]
	public void APickedUpItem_IsPartOfTheCurrentSet_WithoutADuplicateEntry()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, Item(42));
		w.Pickup(w.G1, 42);
		w.Driver.Tick(50);
		Assert.True(w.TransferredOf(w.G1, 42), "the pickup registered the world item for the picker");

		// The current set contains host-known ids as well — the capture states every
		// authoritative item of the body, bound or not — so a repeat reports this one too,
		// and it must stay exactly one entry.
		Register(w.G1, Item(42));
		w.Driver.Tick(50);

		var entries = 0;
		foreach (var transferred in w.Items.GetTransferredItems(w.G1.SteamId))
		{
			if (transferred.Item.InstanceId == 42)
			{
				entries++;
			}
		}

		Assert.True(entries == 1, $"a host-known id keeps exactly one entry after a repeat, got {entries}");
	}

	[Fact]
	public void AnEmptyCaptureAfterANonEmptyRegistration_IsNamedInAWarning()
	{
		// The failure mode this guards: a capture that states nothing while this session has
		// already registered a set. The window would then spend its whole budget sending
		// nothing and the registration would silently stop converging — so it is named.
		var recorder = new RecordingLoggerFactory();
		using var w = ItemSimWorld.Create(s => s.AddSingleton<ILoggerFactory>(recorder));
		var items = w.G1.Services.GetRequiredService<IItemControl>();

		items.ArmCarriedInventoryRegistration("the local generation finished");
		items.SendCarriedInventory([Item(101)]);

		items.SendCarriedInventory([]);
		items.SendCarriedInventory([]); // named once per window, not once per step

		var named = recorder.Entries.Where(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("captured nothing")).ToList();
		Assert.True(named.Count == 1, $"the inert capture must be named exactly once, got {named.Count}");
	}

	[Fact]
	public void AnEmptyCaptureBeforeAnyRegistration_IsNotWarned()
	{
		// A guest that has never registered a set (nothing to state yet — e.g. still loading, or
		// dead with no body) spends its steps quietly. The warning is for an empty capture AFTER a
		// non-empty one, which is the inert-capture shape.
		var recorder = new RecordingLoggerFactory();
		using var w = ItemSimWorld.Create(s => s.AddSingleton<ILoggerFactory>(recorder));
		var items = w.G1.Services.GetRequiredService<IItemControl>();

		items.ArmCarriedInventoryRegistration("the local generation finished");
		items.SendCarriedInventory([]);
		items.SendCarriedInventory([]);

		var named = recorder.Entries.Where(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("captured nothing")).ToList();
		Assert.True(named.Count == 0, $"a capture that never had anything to state must not warn, got {named.Count}");
	}

	/// <summary>
	/// The Game Adapter's pump, minus the Unity capture: while the runtime's cadence
	/// says a report is due, hand it the current carried set. The simulation has no
	/// body to enumerate, so the captured set is the test's — and it CHANGES between
	/// reports exactly like the production capture does (items appear, ids get stamped).
	/// </summary>
	private static void Pump(ItemSimWorld world, TestNode guest, IReadOnlyList<CharacterItemMsg> captured, int ms)
	{
		var items = guest.Services.GetRequiredService<IItemControl>();
		for (var elapsed = 0; elapsed < ms; elapsed += 50)
		{
			world.Driver.Tick(50);
			if (items.IsCarriedInventoryRegistrationDue())
			{
				items.SendCarriedInventory(captured);
			}
		}
	}
}
