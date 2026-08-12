using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// Phase-2 item-domain simulations: the pickup race over the real wire path —
/// two guests claiming the same world item, the host's first-writer-wins
/// arbitration (the transfer + the loser's ItemReject), the lagging reporter
/// (a delayed claim arrives after the winner already took the item) and a
/// seeded random item-lifecycle sequence whose invariants — every host
/// response reaches the reporter, the world table follows the operations —
/// hold under faults. The world construction and injection helpers are shared
/// with the phase-4 replay runner (ItemSimWorld); the replay files fossilize
/// the fixed scenarios, these tests keep the random/property coverage.
/// </summary>
public class ItemSimulationTests
{
	private static CharacterItemMsg Item(string type = "test_item", float condition = 1f) => new()
	{
		ItemId = type,
		Condition = condition,
	};

	[Fact]
	public void PickupRace_TwoGuests_OneWinner_OneReject()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		Assert.True(w.HostTable(42), "the spawn registers in the authoritative table");

		// Both claim the same item — same frame, G1 first.
		w.Pickup(w.G1, 42, Item());
		w.Pickup(w.G2, 42, Item());
		w.Driver.Tick(33);

		// Exactly one winner: the loser's claim comes back as UnknownItem (the
		// item already left the table), the winner's side shows no rejection.
		var g2Rejects = w.Rejects(w.G2);
		var g1Rejects = w.Rejects(w.G1);
		Assert.True(g2Rejects.Count == 1 && g2Rejects[0].ItemId == 42, $"the loser must get exactly one UnknownItem reject, got {g2Rejects.Count}");
		Assert.True(g2Rejects[0].Rejection == ItemRejectMsg.Reason.UnknownItem, "the loser's rollback reason is UnknownItem");
		Assert.Empty(g1Rejects);
		Assert.False(w.HostTable(42), "the item left the world table (transferred to the winner)");
	}

	[Fact]
	public void PickupRace_LaggingReporter_Loses()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		Assert.True(w.HostTable(42));

		// G2's claim is delayed (a congested link); G1's arrives first.
		w.Driver.Network.SetFaults(w.G2.SteamId, w.Host.SteamId, new LinkFaults { DelayMs = 300 });
		w.Pickup(w.G2, 42, Item());
		w.Driver.Tick(33); // G1's claim lands immediately
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(33);

		// G1 won (no reject) while the item was still in the table; G2's delayed
		// claim arrives AFTER the transfer — refused.
		Assert.Empty(w.Rejects(w.G1));
		w.Driver.Tick(300); // deliver the lagging claim
		Assert.True(w.Rejects(w.G2).Any(r => r.ItemId == 42), "the lagging reporter must be refused once the item is gone");
		Assert.False(w.HostTable(42));
	}

	[Theory]
	[InlineData(5)]
	[InlineData(17)]
	[InlineData(29)]
	public void RandomItemSequence_HostResponsesReachTheReporter(int seed)
	{
		using var w = ItemSimWorld.Create();
		var items = w.Items;
		var rng = new Random(seed);
		var spawned = new List<ulong>();
		ulong nextId = 100;

		for (var step = 0; step < 30; step++)
		{
			var roll = rng.NextDouble();
			if (roll < 0.5)
			{
				// Spawn a fresh item (50 %) — the table must register it.
				var id = nextId++;
				w.Spawn(w.G1, id, Item($"type_{id}"));
				spawned.Add(id);
				w.Driver.Tick(33);
				Assert.True(items.IsWorldItemRegistered(id), $"seed {seed} step {step}: spawn {id} must register");
			}
			else if (roll < 0.75 && spawned.Count > 0)
			{
				// Pick a table-present item (25 %): the host either transfers it
				// (a reject would contradict a present entry) or — if another
				// pick already took it — the reject must reach the reporter.
				var id = spawned[rng.Next(spawned.Count)];
				w.Pickup(w.G1, id, Item());
				w.Driver.Tick(33);
				Assert.False(items.IsWorldItemRegistered(id), $"seed {seed} step {step}: pickup {id} must leave the table");
			}
			else if (roll < 0.9 && spawned.Count > 0)
			{
				// Pick up an item that may already be gone (15 % — the racy
				// reporter): whatever the host decides, the response reaches G1.
				var id = spawned[rng.Next(spawned.Count)];
				var before = w.Rejects(w.G1).Count;
				var wasRegistered = items.IsWorldItemRegistered(id);
				w.Pickup(w.G1, id, Item());
				w.Driver.Tick(33);
				if (!wasRegistered)
				{
					Assert.True(w.Rejects(w.G1).Count > before,
						$"seed {seed} step {step}: a claim on a gone item {id} must come back as a reject");
				}
			}
			else if (spawned.Count > 0)
			{
				// Destroy a known item (10 %) — the table must drop it.
				var id = spawned[rng.Next(spawned.Count)];
				if (items.IsWorldItemRegistered(id))
				{
					w.Destroy(w.G1, id);
					w.Driver.Tick(33);
					Assert.False(items.IsWorldItemRegistered(id), $"seed {seed} step {step}: destroy {id} must clear the table");
				}
			}
		}
	}
}
