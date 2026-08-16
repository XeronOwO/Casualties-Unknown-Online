using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// Phase-2 block-break simulations: the first-writer-wins arbitration over the
/// real wire path — the host records the applied air-write (BlockPlaced), the
/// breaker's BlockDamaged report (the drops carrier) consumes it and relays to
/// the other members; a repeated report of the same break is refused (the
/// one-shot record is gone) and the host's executor never double-applies. The
/// executor here is the real <see cref="BlockBreakArbitration"/> machine (the
/// GameAdapter's BlockBreakSync is its thin shell); the relay is the handler's.
/// </summary>
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

	private sealed record SimWorld(SimulationDriver Driver, TestNode Host, TestNode G1, TestNode G2, List<(NetMsg Msg, byte[] Frame)> G2Received, BlockBreakArbitration Arbitration, Counter AcceptedBreaks, Counter AcceptedByG2);

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

		var g2Received = new List<(NetMsg Msg, byte[] Frame)>();
		g2.Transport.MessageReceived += (_, frame) => g2Received.Add(((NetMsg)frame[0], frame));

		var arbitration = new BlockBreakArbitration();
		var accepted = new Counter();
		var acceptedByG2 = new Counter();
		var world = host.Services.GetRequiredService<IWorldControl>();
		world.BlockDamagedReceived += (sender, pos, damage, metalBonus, drops) =>
		{
			// The executor: first-writer-wins — an accepted break relays, a
			// refused one (the record was already consumed) is dropped silently.
			if (!arbitration.TryAccept(sender, (int)Math.Floor(pos.X), (int)Math.Floor(pos.Y)))
			{
				return;
			}

			accepted.Value++;
			if (sender == G2Id)
			{
				acceptedByG2.Value++; // G2's own accepted breaks relay EXCLUDING G2
			}

			world.BroadcastBlockDamaged(sender, pos, damage, metalBonus, drops);
		};

		return new SimWorld(driver, host, g1, g2, g2Received, arbitration, accepted, acceptedByG2);
	}

	private static void ReportBreak(TestNode guest, int cellX, int cellY, List<BlockDropEntryMsg>? drops = null, bool metalBonus = false)
	{
		var sender = guest.Services.GetRequiredService<PacketSender>();
		sender.Send(HostId, NetMsg.BlockDamaged, new BlockDamagedMsg
		{
			Position = new NetVector2Msg(cellX + 0.5f, cellY + 0.5f),
			Damage = 100f,
			MetalBonus = metalBonus,
			Drops = drops,
		});
	}

	[Fact]
	public void FirstBreak_AcceptedAndRelayed_RepeatRefused()
	{
		var w = CreateWorld();

		// The breaker's air-write was applied (BlockPlaced precedes BlockDamaged —
		// both reliable, same source).
		w.Arbitration.RecordAppliedAirWrite(G1Id, 5, 7, now: 0);

		ReportBreak(w.G1, 5, 7);
		w.Driver.Tick(33);

		Assert.True(w.AcceptedBreaks.Value == 1, $"the first break is accepted, got {w.AcceptedBreaks.Value}");
		Assert.True(w.G2Received.Count(r => r.Msg == NetMsg.BlockDamaged) == 1, "the accepted break relays to the other members");

		// A retransmit of the same break: the one-shot record is gone — refused,
		// no second relay (the host would otherwise double-apply the drops).
		ReportBreak(w.G1, 5, 7);
		w.Driver.Tick(33);

		Assert.True(w.AcceptedBreaks.Value == 1, "the repeated break is refused");
		Assert.True(w.G2Received.Count(r => r.Msg == NetMsg.BlockDamaged) == 1, "no second relay");
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

		// Invariant: the OTHER guest received exactly as many relays as the host
		// accepted for the OTHER guest's breaks (each accepted break relays
		// exactly once, source excluded — G2's own accepted breaks never reach
		// it; the duplicates the random sequence unavoidably produced were
		// refused, never double-relayed).
		var relays = w.G2Received.Count(r => r.Msg == NetMsg.BlockDamaged);
		Assert.True(relays == w.AcceptedBreaks.Value - w.AcceptedByG2.Value,
			$"every accepted break relays exactly once to the other members (accepted {w.AcceptedBreaks.Value}, G2's own {w.AcceptedByG2.Value}, relayed {relays})");
	}
}
