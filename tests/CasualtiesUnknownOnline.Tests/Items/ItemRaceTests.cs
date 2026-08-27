using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
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
	public void SpawnPickupInflight_PickupArrivesFirst_SettlesWhenTheSpawnLands()
	{
		// The old branch ItemService.cs:314-318 refused the pickup immediately
		// and left the late spawn in the world for a manual re-pickup. The
		// pending-pickup queue now holds the claim until the spawn report lands
		// (within the 500 ms hold), then settles the SAME transfer the normal
		// spawn-first path would have produced — no reject, no rollback.
		using var w = ItemSimWorld.Create();
		w.Pickup(w.G1, 42, Item()); // clean link — lands immediately, the item is not in the table yet
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { DelayMs = 300 });
		w.Spawn(w.G1, 42, Item()); // delayed — lands while the claim is still held
		w.Driver.Tick(33); // the pickup is queued, no reject yet
		Assert.Empty(w.Rejects(w.G1));
		w.Driver.Tick(300); // the spawn report lands and settles the queue

		Assert.Empty(w.Rejects(w.G1));
		Assert.False(w.HostTable(42), "the settled claim transferred the item out of the world table");
		Assert.True(w.TransferredOf(w.G1, 42), "the picker owns the item exactly like the spawn-first path");
	}

	[Fact]
	public void SpawnPickupInflight_SpawnArrivesAfterTheHold_RejectedThenSpawnRegisters()
	{
		// The queue is bounded: when the registration never arrives inside the
		// hold window the claim gets the late UnknownItem reject, and the even
		// later spawn report registers idempotently (end state: item back in the
		// world, nobody owns it — the old immediate-reject shape, only delayed).
		using var w = ItemSimWorld.Create();
		w.Pickup(w.G1, 42, Item());
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { DelayMs = 700 });
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		w.Driver.Tick(600); // the 500 ms hold expires before the 700 ms spawn lands

		Assert.True(w.Rejects(w.G1).Any(r => r.ItemId == 42), "the unconfirmed claim must be rejected after the hold");
		Assert.True(w.Rejects(w.G1).Count == 1, $"exactly one reject, got {w.Rejects(w.G1).Count}");
		Assert.False(w.HostTable(42), "the item has not registered yet");

		w.Driver.Tick(100); // the late spawn report lands
		Assert.True(w.HostTable(42), "the late spawn report must register idempotently");
		Assert.True(w.Rejects(w.G1).Count == 1, "the late spawn does not produce a second reject");
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
		// oracle that replays the actual delivery order PLUS the pending-pickup
		// hold window (500 ms) at the actual pump ticks — whatever the wire did,
		// every message's effect is the effect of its delivery position and the
		// bounded queue is part of that contract.
		using var w = ItemSimWorld.Create();
		var rng = new Random(seed);
		var delivered = new List<(long Ms, ulong ItemId, NetMsg Msg)>();
		var pumpTicks = new List<long>();
		w.Host.Transport.MessageReceived += (_, frame) => delivered.Add((w.Driver.NowMs, Decode((NetMsg)frame[0], frame).ItemId, Decode((NetMsg)frame[0], frame).Msg));
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
			pumpTicks.Add(w.Driver.NowMs);
		}

		w.Driver.Tick(1000); // every in-flight message landed (max delay 600 ms)
		pumpTicks.Add(w.Driver.NowMs);

		// Oracle: replay the delivery order with the same decisions the host
		// makes — world-table presence, transfer ownership, the duplicate guard,
		// spawn/drop registration settling the first queued claim, and the
		// per-pump expiry of every unconfirmed claim after the 500 ms hold.
		var oracleWorld = new HashSet<ulong>();
		var oracleOwned = new HashSet<ulong>(); // transferred to G1
		var oracleTerminal = new HashSet<ulong>();
		var oracleRejects = new List<ulong>();
		var oracleQueue = new List<(ulong ItemId, long QueuedAtMs)>();
		var eventIndex = 0;

		foreach (var now in pumpTicks)
		{
			while (eventIndex < delivered.Count && delivered[eventIndex].Ms <= now)
			{
				var (_, itemId, msg) = delivered[eventIndex++];
				switch (msg)
				{
					case NetMsg.ItemSpawn:
						if (!oracleTerminal.Contains(itemId))
						{
							SettleSpawn(itemId, oracleWorld, oracleOwned, oracleRejects, oracleQueue);
						}

						break;
					case NetMsg.ItemPickup:
						if (oracleWorld.Remove(itemId))
						{
							oracleOwned.Add(itemId);
						}
						else if (oracleOwned.Contains(itemId) || oracleQueue.Any(q => q.ItemId == itemId))
						{
							// The sender's own retransmit, or a duplicate claim while one is queued — silent.
						}
						else
						{
							oracleQueue.Add((itemId, now));
						}

						break;
					case NetMsg.ItemDrop:
						if (!oracleTerminal.Contains(itemId))
						{
							SettleDrop(itemId, oracleWorld, oracleOwned, oracleRejects, oracleQueue);
						}

						break;
					case NetMsg.ItemDestroy:
						if (oracleWorld.Remove(itemId) || oracleOwned.Remove(itemId))
						{
							oracleTerminal.Add(itemId);
						}

						break;
				}
			}

			ExpireQueue(now, oracleWorld, oracleOwned, oracleRejects, oracleQueue);
		}

		Assert.Equal(delivered.Count, eventIndex); // the final flush landed every in-flight message

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

	/// <summary>The oracle's spawn edge: the first queued claim for the item settles (first-writer-wins), later queued claims lose; otherwise the item registers.</summary>
	private static void SettleSpawn(ulong itemId, HashSet<ulong> world, HashSet<ulong> owned, List<ulong> rejects, List<(ulong ItemId, long QueuedAtMs)> queue)
	{
		var index = queue.FindIndex(q => q.ItemId == itemId);
		if (index < 0)
		{
			world.Add(itemId);
			return;
		}

		queue.RemoveAt(index);
		world.Remove(itemId);
		owned.Add(itemId);
		RejectQueuedLosers(itemId, rejects, queue);
	}

	/// <summary>The oracle's drop edge: the drop leaves G1's transfer entry and registers the item; a queued claim settles exactly like the spawn edge.</summary>
	private static void SettleDrop(ulong itemId, HashSet<ulong> world, HashSet<ulong> owned, List<ulong> rejects, List<(ulong ItemId, long QueuedAtMs)> queue)
	{
		owned.Remove(itemId);
		var index = queue.FindIndex(q => q.ItemId == itemId);
		if (index < 0)
		{
			world.Add(itemId);
			return;
		}

		queue.RemoveAt(index);
		world.Remove(itemId);
		owned.Add(itemId);
		RejectQueuedLosers(itemId, rejects, queue);
	}

	private static void RejectQueuedLosers(ulong itemId, List<ulong> rejects, List<(ulong ItemId, long QueuedAtMs)> queue)
	{
		for (var i = queue.Count - 1; i >= 0; i--)
		{
			if (queue[i].ItemId == itemId)
			{
				queue.RemoveAt(i);
				rejects.Add(itemId);
			}
		}
	}

	/// <summary>The oracle's per-pump expiry edge: a queued claim that outlives the hold rejects, unless its item registered through a non-settling path — then it transfers.</summary>
	private static void ExpireQueue(long now, HashSet<ulong> world, HashSet<ulong> owned, List<ulong> rejects, List<(ulong ItemId, long QueuedAtMs)> queue)
	{
		for (var i = queue.Count - 1; i >= 0; i--)
		{
			if (now - queue[i].QueuedAtMs < PendingPickupQueue.DefaultHoldMs)
			{
				continue;
			}

			var itemId = queue[i].ItemId;
			queue.RemoveAt(i);
			if (world.Remove(itemId))
			{
				owned.Add(itemId);
			}
			else
			{
				rejects.Add(itemId);
			}
		}
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
