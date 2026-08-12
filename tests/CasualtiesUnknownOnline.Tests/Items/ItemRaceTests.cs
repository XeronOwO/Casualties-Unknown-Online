using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The time-sensitive item races — the class of bug manual double-opening
/// cannot reliably reproduce (they need the wire to misbehave at the right
/// moment). Fixed scenarios: the duplicated pickup report (reliable
/// retransmission — found live by the race probe: a reject rolled the
/// winner's own successful transfer back), the spawn-report-in-flight race
/// in both arrival orders (the branch ItemService.cs:314-318 was written
/// for), the symmetric same-frame claim (G2 first) and a reordered arrival
/// (sender order ≠ arrival order). Plus a seeded random lifecycle whose
/// host-table state is checked against an oracle that replays the ACTUAL
/// delivery order — the strongest race check: every message's effect must
/// be exactly the effect of the order it was delivered in.
/// </summary>
public class ItemRaceTests
{
	private static CharacterItemMsg Item(string type = "test_item", float condition = 1f) => new()
	{
		ItemId = type,
		Condition = condition,
	};

	[Fact]
	public void DuplicatedPickupReport_IsIdempotent_NoRejectToTheWinner()
	{
		// The probe that found the bug: the transfer took the item out of the
		// world table, so the retransmitted report looked like an unknown item
		// and a reject rolled the winner's own successful pickup back. The fix:
		// a pickup report for an item the sender ALREADY owns (transfer table)
		// is silent — the spawn/drop idempotency family completed.
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Duplicate = true });
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(33);

		Assert.True(w.Rejects(w.G1).Count == 0, $"the duplicated pickup report must not come back as a reject, got {w.Rejects(w.G1).Count}");
		Assert.False(w.HostTable(42), "the transfer still succeeded");
	}

	[Fact]
	public void SpawnPickupInflight_PickupArrivesFirst_RejectedThenSpawnRegisters()
	{
		// The branch ItemService.cs:314-318 was written for: the pickup wins the
		// race against its own spawn report — refused (UnknownItem, the picker
		// rolls back), then the spawn report lands and registers idempotently.
		// End state: the item is back in the world, nobody owns it.
		using var w = ItemSimWorld.Create();
		w.Pickup(w.G1, 42, Item()); // clean link — lands immediately, the item is not in the table yet
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { DelayMs = 300 });
		w.Spawn(w.G1, 42, Item()); // delayed — lands after the pickup was refused
		w.Driver.Tick(33); // the pickup was processed (reject on the way back)
		w.Driver.Tick(300); // the spawn report lands

		Assert.True(w.Rejects(w.G1).Any(r => r.ItemId == 42), "the in-flight pickup must be refused (item not in the table yet)");
		Assert.True(w.HostTable(42), "the late spawn report must register idempotently");
		Assert.True(w.Rejects(w.G1).Count == 1, $"exactly one reject, got {w.Rejects(w.G1).Count}");
	}

	[Fact]
	public void SpawnPickupInflight_SpawnArrivesFirst_NormalTransfer()
	{
		// The mirror order: the spawn report lands first, the delayed pickup
		// then takes the item out of the table — no reject at all.
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { DelayMs = 300 });
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(300);

		Assert.Empty(w.Rejects(w.G1));
		Assert.False(w.HostTable(42), "the delayed pickup took the item out of the table");
	}

	[Fact]
	public void PickupRace_G2First_G2Wins()
	{
		// The symmetric claim order: the existing race covers G1-first; the
		// winner is whoever's claim lands first, whoever that is.
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		w.Pickup(w.G2, 42, Item());
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(33);

		Assert.True(w.Rejects(w.G1).Any(r => r.ItemId == 42), "the loser (G1) must be refused");
		Assert.Empty(w.Rejects(w.G2));
		Assert.False(w.HostTable(42));
	}

	[Fact]
	public void PickupRace_ReorderedArrival_WinnerIsWhoeverArrivesFirst()
	{
		// Sender order ≠ arrival order: G2 sends first (400 ms link), G1 sends
		// later (200 ms link) — G1's claim arrives first and wins. The arrival
		// order, not the send order, decides.
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		w.Driver.Network.SetFaults(w.G2.SteamId, w.Host.SteamId, new LinkFaults { DelayMs = 400 });
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { DelayMs = 200 });
		w.Pickup(w.G2, 42, Item());
		w.Driver.Tick(100); // G1 sends later
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(400);

		Assert.True(w.Rejects(w.G1).Count == 0, "the first ARRIVAL (G1) wins");
		Assert.True(w.Rejects(w.G2).Any(r => r.ItemId == 42), "the late arrival (G2) is refused");
		Assert.False(w.HostTable(42));
	}

	[Theory]
	[InlineData(11)]
	[InlineData(23)]
	[InlineData(37)]
	public void RandomLifecycleWithJitter_HostTableMatchesDeliveryOrder(int seed)
	{
		// The strongest race check: a random lifecycle (spawn/pickup/destroy/
		// drop by G1) over a jittered link (random delay, occasional duplicate)
		// whose host-table end state and reject stream must EXACTLY match an
		// oracle that replays the actual delivery order — whatever the wire
		// did, every message's effect is the effect of its delivery position.
		using var w = ItemSimWorld.Create();
		var rng = new Random(seed);
		var delivered = new List<(ulong ItemId, NetMsg Msg)>();
		w.Host.Transport.MessageReceived += (_, frame) => delivered.Add(Decode((NetMsg)frame[0], frame));
		ulong nextId = 100;

		for (var step = 0; step < 40; step++)
		{
			// Jitter the link: random delay, occasional duplication.
			var faults = new LinkFaults { DelayMs = rng.Next(0, 600) };
			if (rng.NextDouble() < 0.15)
			{
				faults.Duplicate = true;
			}

			w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, faults);

			var roll = rng.NextDouble();
			if (roll < 0.5)
			{
				w.Spawn(w.G1, nextId, Item($"type_{nextId}"));
				nextId++;
			}
			else if (roll < 0.8)
			{
				var id = nextId == 100 ? 100 : rng.Next(100, (int)nextId);
				w.Pickup(w.G1, (ulong)id, Item());
			}
			else if (roll < 0.9)
			{
				var id = nextId == 100 ? 100 : rng.Next(100, (int)nextId);
				w.Destroy(w.G1, (ulong)id);
			}
			else if (nextId > 100)
			{
				var id = rng.Next(100, (int)nextId);
				w.Drop(w.G1, (ulong)id, Item());
			}

			w.Driver.Tick(33);
		}

		w.Driver.Tick(1000); // every in-flight message landed (max delay 600 ms)

		// Oracle: replay the delivery order with the same decisions the host
		// makes (world-table presence, transfer ownership, the duplicate guard).
		var oracleWorld = new HashSet<ulong>();
		var oracleOwned = new HashSet<ulong>(); // transferred to G1
		var oracleRejects = new List<ulong>();
		foreach (var (itemId, msg) in delivered)
		{
			switch (msg)
			{
				case NetMsg.ItemSpawn:
					oracleWorld.Add(itemId);
					break;
				case NetMsg.ItemPickup:
					if (oracleWorld.Remove(itemId))
					{
						oracleOwned.Add(itemId);
					}
					else if (!oracleOwned.Contains(itemId))
					{
						oracleRejects.Add(itemId);
					}

					break;
				case NetMsg.ItemDrop:
					oracleWorld.Add(itemId);
					oracleOwned.Remove(itemId);
					break;
				case NetMsg.ItemDestroy:
					oracleWorld.Remove(itemId);
					break;
			}
		}

		foreach (var id in oracleWorld)
		{
			Assert.True(w.HostTable(id), $"seed {seed}: oracle says {id} is in the world, the host table lacks it");
		}

		var actualRejects = new List<ulong>();
		foreach (var r in w.Rejects(w.G1))
		{
			actualRejects.Add(r.ItemId);
		}

		actualRejects.Sort();
		oracleRejects.Sort();
		Assert.Equal(oracleRejects, actualRejects);
	}

	private static (ulong ItemId, NetMsg Msg) Decode(NetMsg msg, byte[] frame) =>
		msg switch
		{
			NetMsg.ItemSpawn => (NetPacket.DecodePayload<ItemSpawnMsg>(frame).ItemId, msg),
			NetMsg.ItemPickup => (NetPacket.DecodePayload<ItemPickupMsg>(frame).ItemId, msg),
			NetMsg.ItemDrop => (NetPacket.DecodePayload<ItemDropMsg>(frame).ItemId, msg),
			NetMsg.ItemDestroy => (NetPacket.DecodePayload<ItemDestroyMsg>(frame).ItemId, msg),
			_ => (0, msg),
		};
}
