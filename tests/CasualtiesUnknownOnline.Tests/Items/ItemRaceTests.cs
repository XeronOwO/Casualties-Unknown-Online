using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Protocol.Wire;
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
/// retransmission), the spawn-report-in-flight race in both arrival orders
/// (the kernel pending-pickup queue), the symmetric same-frame claim (G2
/// first) and a reordered arrival (sender order ≠ arrival order). Plus a
/// seeded random lifecycle whose host-table state is checked against an oracle
/// that replays the ACTUAL delivery order of the Phase C commands.
/// </summary>
public class ItemRaceTests
{
	private static CharacterItemMsg Item(string type = "test_item", float condition = 1f) => new()
	{
		ItemId = type,
		Condition = condition,
	};

	[Fact]
	public void OwnSpawnEcho_SurfacesExactlyOneSpawnEvent()
	{
		// The host broadcasts the committed batch back to the reporting guest
		// (originator included). The runtime event surface must be exactly one
		// ItemSpawned; the GameAdapter materialization additionally has to
		// reuse the already-present local original (see RemoteWorldItemSpawn
		// same-id self-check) instead of creating a second scene object.
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);

		Assert.Equal(1, w.SpawnedEvents(w.G1));
	}

	[Fact]
	public void DuplicatedPickupReport_IsIdempotent_NoRejectToTheWinner()
	{
		// A Steam-reliable retransmit duplicates the same CommandEnvelope, so
		// the kernel sees the same OperationId twice and applies the pickup
		// only once.
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
		// The kernel pending-pickup queue holds the claim until the spawn
		// command lands (within the 500 ms hold), then settles the same
		// transfer the spawn-first path would have produced.
		using var w = ItemSimWorld.Create();
		w.Pickup(w.G1, 42, Item());
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { DelayMs = 300 });
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33); // the pickup is queued, no reject yet
		Assert.Empty(w.Rejects(w.G1));
		w.Driver.Tick(300); // the spawn command lands and settles the queue

		Assert.Empty(w.Rejects(w.G1));
		Assert.False(w.HostTable(42), "the settled claim transferred the item out of the world table");
		Assert.True(w.TransferredOf(w.G1, 42), "the picker owns the item exactly like the spawn-first path");
	}

	[Fact]
	public void SpawnPickupInflight_SpawnArrivesAfterTheHold_RejectedThenSpawnRegisters()
	{
		// The queue is bounded: when the registration never arrives inside the
		// hold window the claim gets the late UnknownItem reject, and the even
		// later spawn command registers idempotently.
		using var w = ItemSimWorld.Create();
		w.Pickup(w.G1, 42, Item());
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { DelayMs = 700 });
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		w.Driver.Tick(600); // the 500 ms hold expires before the 700 ms spawn lands

		Assert.True(w.Rejects(w.G1).Any(r => r.ItemId == 42), "the unconfirmed claim must be rejected after the hold");
		Assert.True(w.Rejects(w.G1).Count == 1, $"exactly one reject, got {w.Rejects(w.G1).Count}");
		Assert.False(w.HostTable(42), "the item has not registered yet");

		w.Driver.Tick(100); // the late spawn command lands
		Assert.True(w.HostTable(42), "the late spawn command must register idempotently");
		Assert.True(w.Rejects(w.G1).Count == 1, "the late spawn does not produce a second reject");
	}

	[Fact]
	public void SpawnPickupInflight_SpawnArrivesFirst_NormalTransfer()
	{
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
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		w.Driver.Network.SetFaults(w.G2.SteamId, w.Host.SteamId, new LinkFaults { DelayMs = 400 });
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { DelayMs = 200 });
		w.Pickup(w.G2, 42, Item());
		w.Driver.Tick(100);
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
		// oracle that replays the actual delivery order of the Phase C commands
		// PLUS the kernel pending-pickup hold window (500 ms) at the actual pump
		// ticks.
		using var w = ItemSimWorld.Create();
		var rng = new Random(seed);
		var delivered = new List<(long Ms, ulong ItemId, WireCommandKind Kind, ulong OperationId)>();
		var pumpTicks = new List<long>();
		w.Host.Transport.MessageReceived += (_, frame) =>
		{
			if ((NetMsg)frame[0] != NetMsg.KernelEnvelope)
			{
				return;
			}

			var envelope = NetPacket.DecodePayload<ProtocolFrame>(frame);
			var commandEnvelope = envelope.Command;
			var command = commandEnvelope?.Command;
			if (command is null || commandEnvelope is null)
			{
				return;
			}

			delivered.Add((w.Driver.NowMs, command.Identity.InstanceId, command.Kind, commandEnvelope.Header.OperationId));
		};
		ulong nextId = 100;

		for (var step = 0; step < 40; step++)
		{
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

		w.Driver.Tick(1000);
		pumpTicks.Add(w.Driver.NowMs);

		var oracleWorld = new HashSet<ulong>();
		var oracleOwned = new HashSet<ulong>();
		var oracleTerminal = new HashSet<ulong>();
		var oracleRejects = new List<ulong>();
		var oracleQueue = new List<(ulong ItemId, long QueuedAtMs)>();
		var seenOperations = new HashSet<ulong>();
		var eventIndex = 0;

		foreach (var now in pumpTicks)
		{
			while (eventIndex < delivered.Count && delivered[eventIndex].Ms <= now)
			{
				var (_, itemId, kind, operationId) = delivered[eventIndex++];
				if (seenOperations.Contains(operationId))
				{
					continue; // accepted operations are idempotent by OperationId
				}

				var accepted = false;
				switch (kind)
				{
					case WireCommandKind.ItemSpawn:
						if (!oracleTerminal.Contains(itemId))
						{
							SettleSpawn(itemId, oracleWorld, oracleOwned, oracleRejects, oracleQueue);
							accepted = true;
						}

						break;
					case WireCommandKind.ItemPickup:
						if (oracleTerminal.Contains(itemId))
						{
							oracleRejects.Add(itemId);
						}
						else if (oracleWorld.Remove(itemId))
						{
							oracleOwned.Add(itemId);
							accepted = true;
						}
						else if (oracleOwned.Contains(itemId))
						{
							oracleRejects.Add(itemId);
						}
						else if (oracleQueue.Any(q => q.ItemId == itemId))
						{
							// A duplicate claim while one is queued is dropped silently.
						}
						else
						{
							oracleQueue.Add((itemId, now));
						}

						break;
					case WireCommandKind.ItemDrop:
						if (oracleTerminal.Contains(itemId))
						{
							oracleRejects.Add(itemId);
						}
						else if (!oracleOwned.Contains(itemId) && !oracleWorld.Contains(itemId))
						{
							oracleRejects.Add(itemId);
						}
						else
						{
							SettleDrop(itemId, oracleWorld, oracleOwned, oracleRejects, oracleQueue);
							accepted = true;
						}

						break;
					case WireCommandKind.ItemDestroy:
						if (oracleWorld.Remove(itemId) || oracleOwned.Remove(itemId))
						{
							oracleTerminal.Add(itemId);
							accepted = true;
						}
						else
						{
							oracleRejects.Add(itemId);
						}

						break;
				}

				if (accepted)
				{
					seenOperations.Add(operationId);
				}
			}

			ExpireQueue(now, oracleWorld, oracleOwned, oracleRejects, oracleQueue);
		}

		Assert.Equal(delivered.Count, eventIndex);

		foreach (var id in oracleWorld)
		{
			Assert.True(w.HostTable(id), $"seed {seed}: oracle says {id} is in the world, the host table lacks it");
		}

		var actualRejects = w.Rejects(w.G1).Select(r => r.ItemId).ToList();
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
}
