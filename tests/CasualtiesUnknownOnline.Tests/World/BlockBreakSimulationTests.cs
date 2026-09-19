using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// Phase-2 block-break simulations: the first-writer-wins arbitration over the
/// real wire path — the host records the applied air-write (BlockPlaced), the
/// breaker's BlockDamaged report (the drops carrier) consumes it and relays to
/// the other members; a repeated report is REFUSED only when the cell's break
/// belongs to someone else. Since the break-drop recovery landed, a repeat from
/// the SAME breaker is an idempotent re-accept (its 60 s fallback re-sends the
/// break when the acknowledgement relay was the lost message), while a second
/// breaker of the same cell stays refused. The executor here is the real
/// <see cref="BlockBreakArbitration"/> machine (the GameAdapter's BlockBreakSync
/// is its thin shell); the relay is the handler's.
/// </summary>
[Trait("Category", "Integration")]
public class BlockBreakSimulationTests
{
	private const ulong HostId = 1001;
	private const ulong G1Id = 2001;
	private const ulong G2Id = 3001;
	private const ulong LobbyId = 9001;

	/// <summary>Mutable counter — the record's snapshot value would never advance.</summary>
	private sealed class Counter
	{
		internal int Value;
	}

	private sealed record SimWorld(SimulationDriver Driver, TestNode Host, TestNode G1, TestNode G2, List<(NetMsg Msg, byte[] Frame)> G1Received, List<(NetMsg Msg, byte[] Frame)> G2Received, BlockBreakArbitration Arbitration, Counter AcceptedBreaks, Counter AcceptedByG2, Counter Relays);

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
		g1.Transport.MessageReceived += (_, frame) => g1Received.Add(((NetMsg)frame[0], frame));
		var g2Received = new List<(NetMsg Msg, byte[] Frame)>();
		g2.Transport.MessageReceived += (_, frame) => g2Received.Add(((NetMsg)frame[0], frame));

		var arbitration = new BlockBreakArbitration();
		var accepted = new Counter();
		var acceptedByG2 = new Counter();
		var relays = new Counter(); // every relay the host sent (the relay includes the reporter)
		var world = host.Services.GetRequiredService<IWorldControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		world.BlockDamagedReceived += (sender, pos, damage, metalBonus, drops, buildingDrops, generation) =>
		{
			// The executor: first-writer-wins — an accepted break relays, a
			// refused one (the cell's break belongs to another writer) rolls every
			// drop back to the breaker via ItemReject(BlockAlreadyBroken). A repeat
			// from the same breaker is acknowledged idempotently instead: it is the
			// breaker's own recovery re-report, and the registration/materialization
			// of those drops is idempotent per item id.
			// The sim has no live world and no run baseline: a recorded air write is
			// the only cell model it has, so the standing-block shape cannot arise
			// here (GuestBreakDropRecoveryTests covers it with a real cell state and
			// a real generation stamp).
			var verdict = arbitration.TryAccept(sender, (int)Math.Floor(pos.X), (int)Math.Floor(pos.Y), cellIsAir: true, generationVerified: false);
			if (verdict == Verdict.Refused)
			{
				if (drops is not null)
				{
					foreach (var drop in drops)
					{
						items.SendItemReject(sender, drop.ItemId, ItemRejectMsg.Reason.BlockAlreadyBroken);
					}
				}

				if (buildingDrops is not null)
				{
					foreach (var drop in buildingDrops)
					{
						items.SendItemReject(sender, drop.ItemId, ItemRejectMsg.Reason.BlockAlreadyBroken);
					}
				}

				return;
			}

			arbitration.RecordAccepted(sender, (int)Math.Floor(pos.X), (int)Math.Floor(pos.Y), now: 0f); // the break is real
			accepted.Value++;
			relays.Value++;
			if (sender == G2Id)
			{
				acceptedByG2.Value++;
			}

			items.FireBlockDropsReceived(sender, drops ?? []);
			items.FireBuildingDropsReceived(sender, buildingDrops ?? []);
			world.BroadcastBlockDamaged(0, pos, damage, metalBonus, drops, buildingDrops); // everyone, the reporter included — that echo is the acknowledgement
		};

		return new SimWorld(driver, host, g1, g2, g1Received, g2Received, arbitration, accepted, acceptedByG2, relays);
	}

	private static void ReportBreak(TestNode guest, int cellX, int cellY, List<BlockDropEntryMsg>? drops = null, List<TrapDropEntryMsg>? buildingDrops = null, bool metalBonus = false)
	{
		var sender = guest.Services.GetRequiredService<PacketSender>();
		sender.Send(HostId, NetMsg.BlockDamaged, new BlockDamagedMsg
		{
			Position = new NetVector2Msg(cellX + 0.5f, cellY + 0.5f),
			Damage = 100f,
			MetalBonus = metalBonus,
			Drops = drops,
			BuildingDrops = buildingDrops,
		});
	}

	[Fact]
	public void FirstBreak_AcceptedAndRelayed_RepeatFromTheSameBreakerReRelays()
	{
		var w = CreateWorld();

		// The breaker's air-write was applied (BlockPlaced precedes BlockDamaged —
		// both reliable, same source).
		w.Arbitration.RecordAppliedAirWrite(G1Id, 5, 7, now: 0);

		ReportBreak(w.G1, 5, 7);
		w.Driver.Tick(33);

		Assert.True(w.AcceptedBreaks.Value == 1, $"the first break is accepted, got {w.AcceptedBreaks.Value}");
		Assert.True(w.G2Received.Count(r => r.Msg == NetMsg.BlockDamaged) == 1, "the accepted break relays to the other members");

		// The breaker's 60 s fallback re-sends the break (its acknowledgement relay
		// was the lost message): the host re-relays instead of refusing, and the
		// relay now reaches the reporter too — that echo IS the acknowledgement
		// that clears its pending drop report. Re-registering the same item ids is
		// idempotent, so a repeat can never double-materialize.
		ReportBreak(w.G1, 5, 7);
		w.Driver.Tick(33);

		Assert.True(w.AcceptedBreaks.Value == 2, "the repeat from the same breaker is re-accepted");
		Assert.True(w.G2Received.Count(r => r.Msg == NetMsg.BlockDamaged) == 2, "the repeat re-relays — the acknowledgement can be the lost message");
		Assert.True(w.G1Received.Count(r => r.Msg == NetMsg.BlockDamaged) >= 1, "the relay must reach the reporter — its echo is the acknowledgement");
	}

	[Fact]
	public void OtherBreakersReport_OfAnAcceptedCell_IsRefused()
	{
		var w = CreateWorld();

		// G1's air-write applied and its break was accepted: the cell is G1's.
		w.Arbitration.RecordAppliedAirWrite(G1Id, 5, 7, now: 0);
		ReportBreak(w.G1, 5, 7, drops: [new BlockDropEntryMsg { ItemId = 77 }]);
		w.Driver.Tick(33);
		Assert.True(w.AcceptedBreaks.Value == 1, "the first break is accepted");

		// G2's report for the same cell has no record of its own — first-writer-wins
		// refuses it and rolls its drops back; it is never mistaken for a repeat of
		// G1's break (the accepted record is per sender).
		var rejects = new List<(ulong ItemId, ItemRejectMsg.Reason Reason)>();
		w.G2.Services.GetRequiredService<ItemService>().ItemRejected += (itemId, reason) => rejects.Add((itemId, reason));
		ReportBreak(w.G2, 5, 7, drops: [new BlockDropEntryMsg { ItemId = 88 }]);
		w.Driver.Tick(33);

		Assert.True(w.AcceptedBreaks.Value == 1, "another sender's report is refused");
		var reject = Assert.Single(rejects);
		Assert.True(reject.ItemId == 88 && reject.Reason == ItemRejectMsg.Reason.BlockAlreadyBroken,
			$"the loser must roll its drops back, got item {reject.ItemId} reason {reject.Reason}");
	}

	[Fact]
	public void BreakWithoutAirWriteRecord_Refused()
	{
		var w = CreateWorld();

		ReportBreak(w.G1, 9, 9); // no recorded air-write — the drops cannot be attributed
		w.Driver.Tick(33);

		Assert.True(w.AcceptedBreaks.Value == 0, "an unattributed break is refused");
		Assert.DoesNotContain(w.G2Received, r => r.Msg == NetMsg.BlockDamaged);
	}

	[Fact]
	public void DropsRideTheAcceptedBreak()
	{
		var w = CreateWorld();
		w.Arbitration.RecordAppliedAirWrite(G1Id, 3, 4, now: 0);

		ReportBreak(w.G1, 3, 4, drops: [new BlockDropEntryMsg { ItemId = 77 }]);
		w.Driver.Tick(33);

		var relay = w.G2Received.Single(r => r.Msg == NetMsg.BlockDamaged).Frame;
		var msg = NetPacket.DecodePayload<BlockDamagedMsg>(relay);
		Assert.True(msg.Drops != null && msg.Drops.Count == 1 && msg.Drops[0].ItemId == 77, "the accepted break's drops ride the relay");
	}

	[Fact]
	public void BuildingDropsRideTheAcceptedBreakRelay()
	{
		var w = CreateWorld();
		w.Arbitration.RecordAppliedAirWrite(G1Id, 3, 4, now: 0);

		ReportBreak(w.G1, 3, 4,
			buildingDrops:
			[
				new TrapDropEntryMsg
				{
					ItemId = 90,
					Item = new CharacterItemMsg { ItemId = "metalscrap", Condition = 1f },
					Position = new NetVector2Msg(3.5f, 4.5f),
					Velocity = new NetVector2Msg(1f, -2f),
					Rotation = 45f,
					FreshItemDrop = true,
					AngularVelocity = 8f,
				},
			]);
		w.Driver.Tick(33);

		var relay = w.G2Received.Single(r => r.Msg == NetMsg.BlockDamaged).Frame;
		var msg = NetPacket.DecodePayload<BlockDamagedMsg>(relay);
		var drop = Assert.Single(msg.BuildingDrops!);
		Assert.Equal(90ul, drop.ItemId);
		Assert.Equal("metalscrap", drop.Item.ItemId);
		Assert.True(drop.FreshItemDrop);
		Assert.Equal(1f, drop.Velocity.X);
		Assert.Equal(-2f, drop.Velocity.Y);
	}

	[Fact]
	public void MetalBonus_RidesTheAcceptedBreakRelay()
	{
		var w = CreateWorld();
		w.Arbitration.RecordAppliedAirWrite(G1Id, 4, 4, now: 0);

		ReportBreak(w.G1, 4, 4, drops: [new BlockDropEntryMsg { ItemId = 78 }], metalBonus: true);
		w.Driver.Tick(33);

		// The accepted break's relay must preserve the bonus flag — the peer's
		// DamageBlock applies the game's ×10 metallic multiplier from it.
		var relay = w.G2Received.Single(r => r.Msg == NetMsg.BlockDamaged).Frame;
		var msg = NetPacket.DecodePayload<BlockDamagedMsg>(relay);
		Assert.True(msg.MetalBonus, "the accepted break's relay must carry the source's bonus-metal flag");
	}

	[Fact]
	public void TwoGuestsBreakSameCellAtTheSameTime_FirstWriterWins_LoserRejected()
	{
		var w = CreateWorld();
		var rejects = new List<(ulong ItemId, ItemRejectMsg.Reason Reason)>();
		w.G2.Services.GetRequiredService<ItemService>().ItemRejected += (itemId, reason) => rejects.Add((itemId, reason));

		// G1's air-write applied first — the host's OnRemoteBlockPlaced refuses
		// G2's same-cell air-write as already-broken, so only G1 has the
		// first-writer record. Both breaks then arrive in the same tick.
		w.Arbitration.RecordAppliedAirWrite(G1Id, 5, 7, now: 0);
		ReportBreak(w.G1, 5, 7, drops: [new BlockDropEntryMsg { ItemId = 77 }]);
		ReportBreak(w.G2, 5, 7, drops: [new BlockDropEntryMsg { ItemId = 88 }]);
		w.Driver.Tick(33);

		Assert.True(w.AcceptedBreaks.Value == 1, $"two guests breaking the same cell must yield exactly one winner, got {w.AcceptedBreaks.Value}");
		var relay = w.G2Received.Single(r => r.Msg == NetMsg.BlockDamaged).Frame;
		var relayMsg = NetPacket.DecodePayload<BlockDamagedMsg>(relay);
		Assert.True(relayMsg.Drops != null && relayMsg.Drops.Count == 1 && relayMsg.Drops[0].ItemId == 77,
			"the relayed break must be the winner's drops (77), not the loser's (88)");
		var reject = Assert.Single(rejects);
		Assert.True(reject.ItemId == 88 && reject.Reason == ItemRejectMsg.Reason.BlockAlreadyBroken,
			$"the loser must roll its drops back with BlockAlreadyBroken, got item {reject.ItemId} reason {reject.Reason}");
	}

	[Theory]
	[InlineData(3)]
	[InlineData(11)]
	[InlineData(19)]
	public void RandomBreakSequence_EveryAcceptedBreakRelays_Once(int seed)
	{
		var w = CreateWorld();
		var rng = new Random(seed);

		for (var step = 0; step < 25; step++)
		{
			var cellX = rng.Next(0, 20);
			var cellY = rng.Next(0, 20);
			var breaker = rng.NextDouble() < 0.5 ? w.G1 : w.G2;
			if (rng.NextDouble() < 0.6)
			{
				// The air-write landed (60 %) — the break may be accepted.
				w.Arbitration.RecordAppliedAirWrite(breaker.SteamId, cellX, cellY, now: 0);
			}

			ReportBreak(breaker, cellX, cellY);
			w.Driver.Tick(33);
		}

		// Invariant: every accepted report relays exactly once and the relay reaches
		// EVERY member — the reporter included, because that echo is the
		// acknowledgement that clears its pending drop report. The duplicates the
		// random sequence unavoidably produced were refused, never double-relayed.
		var g2Relays = w.G2Received.Count(r => r.Msg == NetMsg.BlockDamaged);
		Assert.True(g2Relays == w.Relays.Value,
			$"every accepted report relays once, to every member (relays {w.Relays.Value}, G2 received {g2Relays})");
	}
}
