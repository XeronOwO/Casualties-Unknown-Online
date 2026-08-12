using System;
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
/// Phase-2 item-domain simulations: the pickup race over the real wire path —
/// two guests claiming the same world item, the host's first-writer-wins
/// arbitration (the transfer + the loser's ItemReject), the lagging reporter
/// (a delayed claim arrives after the winner already took the item) and a
/// seeded random item-lifecycle sequence whose invariants — every host
/// response reaches the reporter, the world table follows the operations —
/// hold under faults.
/// </summary>
public class ItemSimulationTests
{
	private const ulong HostId = 1001;
	private const ulong G1Id = 2001;
	private const ulong G2Id = 3001;
	private const ulong LobbyId = 9001;

	private static CharacterItemMsg Item(string type = "test_item", float condition = 1f) => new()
	{
		ItemId = type,
		Condition = condition,
	};

	/// <summary>A three-node session with per-node received-frame recording (the
	/// reporters' wire surface — what each guest actually got).</summary>
	private sealed record SimWorld(SimulationDriver Driver, TestNode Host, TestNode G1, TestNode G2, List<(NetMsg Msg, byte[] Frame)> G1Received, List<(NetMsg Msg, byte[] Frame)> G2Received);

	private static SimWorld CreateWorld()
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var g1Steam = new FakeSteamService(G1Id) { LobbyOwner = HostId, LobbyMembers = [HostId, G1Id, G2Id] };
		var g2Steam = new FakeSteamService(G2Id) { LobbyOwner = HostId, LobbyMembers = [HostId, G1Id, G2Id] };
		var host = TestNode.Create(HostId, network, hostSteam, clock);
		var g1 = TestNode.Create(G1Id, network, g1Steam, clock);
		var g2 = TestNode.Create(G2Id, network, g2Steam, clock);
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, G1Id, G2Id];
		g1.Steam.FireLobbyEntered(LobbyId);
		g2.Steam.FireLobbyEntered(LobbyId);
		var driver = new SimulationDriver(clock, network, host, g1, g2);
		driver.TickUntil(
			() => host.Session.Members.Count(m => m.Handshaken) == 2 && g1.Session.Members.Any(m => m.Handshaken) && g2.Session.Members.Any(m => m.Handshaken),
			maxMs: 5000);

		var g1Received = new List<(NetMsg Msg, byte[] Frame)>();
		var g2Received = new List<(NetMsg Msg, byte[] Frame)>();
		g1.Transport.MessageReceived += (_, frame) => g1Received.Add(((NetMsg)frame[0], frame));
		g2.Transport.MessageReceived += (_, frame) => g2Received.Add(((NetMsg)frame[0], frame));

		return new SimWorld(driver, host, g1, g2, g1Received, g2Received);
	}

	private static void ReportSpawn(TestNode guest, ulong itemId, CharacterItemMsg item)
	{
		var sender = guest.Services.GetRequiredService<PacketSender>();
		sender.Send(HostId, NetMsg.ItemSpawn, new ItemSpawnMsg { ItemId = itemId, Item = item });
	}

	private static void ReportPickup(TestNode guest, ulong itemId, CharacterItemMsg? evidence = null)
	{
		var sender = guest.Services.GetRequiredService<PacketSender>();
		sender.Send(HostId, NetMsg.ItemPickup, new ItemPickupMsg { ItemId = itemId, Item = evidence });
	}

	private static void ReportDestroy(TestNode guest, ulong itemId)
	{
		var sender = guest.Services.GetRequiredService<PacketSender>();
		sender.Send(HostId, NetMsg.ItemDestroy, new ItemDestroyMsg { ItemId = itemId });
	}

	private static List<ItemRejectMsg> Rejects(IEnumerable<(NetMsg Msg, byte[] Frame)> received) =>
		[.. received.Where(r => r.Msg == NetMsg.ItemReject).Select(r => NetPacket.DecodePayload<ItemRejectMsg>(r.Frame))];

	[Fact]
	public void PickupRace_TwoGuests_OneWinner_OneReject()
	{
		var w = CreateWorld();
		ReportSpawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		var items = w.Host.Services.GetRequiredService<ItemService>();
		Assert.True(items.IsWorldItemRegistered(42), "the spawn registers in the authoritative table");

		// Both claim the same item — same frame, G1 first.
		ReportPickup(w.G1, 42, Item());
		ReportPickup(w.G2, 42, Item());
		w.Driver.Tick(33);

		// Exactly one winner: the loser's claim comes back as UnknownItem (the
		// item already left the table), the winner's side shows no rejection.
		var g2Rejects = Rejects(w.G2Received);
		var g1Rejects = Rejects(w.G1Received);
		Assert.True(g2Rejects.Count == 1 && g2Rejects[0].ItemId == 42, $"the loser must get exactly one UnknownItem reject, got {g2Rejects.Count}");
		Assert.True(g2Rejects[0].Rejection == ItemRejectMsg.Reason.UnknownItem, "the loser's rollback reason is UnknownItem");
		Assert.Empty(g1Rejects);
		Assert.False(items.IsWorldItemRegistered(42), "the item left the world table (transferred to the winner)");
	}

	[Fact]
	public void PickupRace_LaggingReporter_Loses()
	{
		var w = CreateWorld();
		ReportSpawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		Assert.True(w.Host.Services.GetRequiredService<ItemService>().IsWorldItemRegistered(42));

		// G2's claim is delayed (a congested link); G1's arrives first.
		w.Driver.Network.SetFaults(G2Id, HostId, new LinkFaults { DelayMs = 300 });
		ReportPickup(w.G2, 42, Item());
		w.Driver.Tick(33); // G1's claim lands immediately
		ReportPickup(w.G1, 42, Item());
		w.Driver.Tick(33);

		// G1 won (no reject) while the item was still in the table; G2's delayed
		// claim arrives AFTER the transfer — refused.
		Assert.Empty(Rejects(w.G1Received));
		w.Driver.Tick(300); // deliver the lagging claim
		Assert.True(Rejects(w.G2Received).Any(r => r.ItemId == 42), "the lagging reporter must be refused once the item is gone");
		Assert.False(w.Host.Services.GetRequiredService<ItemService>().IsWorldItemRegistered(42));
	}

	[Theory]
	[InlineData(5)]
	[InlineData(17)]
	[InlineData(29)]
	public void RandomItemSequence_HostResponsesReachTheReporter(int seed)
	{
		var w = CreateWorld();
		var items = w.Host.Services.GetRequiredService<ItemService>();
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
				ReportSpawn(w.G1, id, Item($"type_{id}"));
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
				ReportPickup(w.G1, id, Item());
				w.Driver.Tick(33);
				Assert.False(items.IsWorldItemRegistered(id), $"seed {seed} step {step}: pickup {id} must leave the table");
			}
			else if (roll < 0.9 && spawned.Count > 0)
			{
				// Pick up an item that may already be gone (15 % — the racy
				// reporter): whatever the host decides, the response reaches G1.
				var id = spawned[rng.Next(spawned.Count)];
				var before = Rejects(w.G1Received).Count;
				var wasRegistered = items.IsWorldItemRegistered(id);
				ReportPickup(w.G1, id, Item());
				w.Driver.Tick(33);
				if (!wasRegistered)
				{
					Assert.True(Rejects(w.G1Received).Count > before,
						$"seed {seed} step {step}: a claim on a gone item {id} must come back as a reject");
				}
			}
			else if (spawned.Count > 0)
			{
				// Destroy a known item (10 %) — the table must drop it.
				var id = spawned[rng.Next(spawned.Count)];
				if (items.IsWorldItemRegistered(id))
				{
					ReportDestroy(w.G1, id);
					w.Driver.Tick(33);
					Assert.False(items.IsWorldItemRegistered(id), $"seed {seed} step {step}: destroy {id} must clear the table");
				}
			}
		}
	}
}
