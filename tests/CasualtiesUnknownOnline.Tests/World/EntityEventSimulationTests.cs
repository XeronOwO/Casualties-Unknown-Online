using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// Phase-2 entity-event simulations over a THREE-node star (host + two guests):
/// the relay topology (source excluded, the other members get exactly one
/// copy), the host-side one-shot consumption (the TrapConsumptionRegistry — the
/// late-joiner snapshot's fact source — and the per-entity duplicate guard the
/// production executor applies), the runtime-spawn relay, and the fluid
/// channel's unreliable absolute-region semantics (a lost region is healed by
/// the next one; the interaction relay excludes the source).
/// </summary>
public class EntityEventSimulationTests
{
	private const ulong HostId = 1001;
	private const ulong G1Id = 2001;
	private const ulong G2Id = 3001;
	private const ulong LobbyId = 9001;

	/// <summary>Mutable execution counter — the record's snapshot value would never advance.</summary>
	private sealed class ExecutionCounter
	{
		internal int Value;
	}

	private sealed record SimWorld(SimulationDriver Driver, TestNode Host, TestNode G1, TestNode G2, List<EntityEventMsg> G1Events, List<EntityEventMsg> G2Events, ExecutionCounter HostExecutions);

	/// <summary>
	/// Three fully-handshaken nodes. The host executor (the production
	/// TrapEffectApplier's shape): an event arrives → the one-shot consumption is
	/// recorded (TrapConsumptionRegistry — the real Runtime service) unless the
	/// per-entity duplicate guard rejects it. The RELAY is not the executor's
	/// job: the EntityEventHandler relays automatically (BroadcastExcept, source
	/// excluded) — the executor only applies to the host's own world.
	/// </summary>
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

		var g1Events = new List<EntityEventMsg>();
		var g2Events = new List<EntityEventMsg>();
		g1.Services.GetRequiredService<IWorldControl>().EntityEventReceived += (_, msg) => g1Events.Add(msg);
		g2.Services.GetRequiredService<IWorldControl>().EntityEventReceived += (_, msg) => g2Events.Add(msg);

		var world = host.Services.GetRequiredService<IWorldControl>();
		var registry = host.Services.GetRequiredService<TrapConsumptionRegistry>();
		var executed = new HashSet<(EntityEventKind Kind, int X, int Y)>(); // the per-entity one-shot guard
		var hostExecutions = new ExecutionCounter();
		world.EntityEventReceived += (_, msg) =>
		{
			var key = ((int)Math.Floor(msg.Position.X), (int)Math.Floor(msg.Position.Y));
			if (EntityEventProfiles.IsOneShotConsumption(msg.Kind) && !executed.Add((msg.Kind, key.Item1, key.Item2)))
			{
				return; // duplicate — the production per-entity guard drops the re-execution
			}

			hostExecutions.Value++; // a consumption actually executed
			registry.Report(msg.Kind, msg.Position.X, msg.Position.Y, msg.Extra);
		};

		return new SimWorld(driver, host, g1, g2, g1Events, g2Events, hostExecutions);
	}

	private static EntityEventMsg Event(EntityEventKind kind, float x, float y, byte extra = 0) =>
		new() { Kind = kind, Position = new NetVector2Msg(x, y), Extra = extra };

	[Fact]
	public void GuestTrigger_RelayedToOtherGuest_SourceExcluded()
	{
		var w = CreateWorld();

		w.G1.Services.GetRequiredService<IWorldControl>().SendEntityEvent(Event(EntityEventKind.MineExploded, 10f, 20f));

		Assert.True(w.G2Events.Count == 1,
			$"the other guest must get exactly one copy, got {w.G2Events.Count} (host executed {w.HostExecutions} time(s))");
		Assert.True(w.G2Events[0].Kind == EntityEventKind.MineExploded, "the relay carries the event");
		Assert.True(w.G2Events[0].Position.X == 10f && w.G2Events[0].Position.Y == 20f, "the position key rides through");
		Assert.Empty(w.G1Events);
	}

	[Fact]
	public void HostTrigger_BroadcastToEveryGuest()
	{
		var w = CreateWorld();

		w.Host.Services.GetRequiredService<IWorldControl>().SendEntityEvent(Event(EntityEventKind.SpikeStabbed, 5f, 5f));

		Assert.True(w.G1Events.Count == 1 && w.G2Events.Count == 1, $"both guests must get one copy (g1: {w.G1Events.Count}, g2: {w.G2Events.Count})");
	}

	[Fact]
	public void DuplicateReport_GuardDropsTheSecondExecution_ConsumptionStaysOne()
	{
		var w = CreateWorld();

		w.G1.Services.GetRequiredService<IWorldControl>().SendEntityEvent(Event(EntityEventKind.MineExploded, 10f, 20f));
		w.G1.Services.GetRequiredService<IWorldControl>().SendEntityEvent(Event(EntityEventKind.MineExploded, 10f, 20f)); // a retransmit

		// The handler relays unconditionally (the message layer is not the guard —
		// the relayed duplicate is what the guests' own replay guards consume).
		Assert.True(w.G2Events.Count == 2, $"both reports relay (the replay guard lives on the receiving side), got {w.G2Events.Count}");
		Assert.True(w.HostExecutions.Value == 1, $"the HOST executes the consumption once, got {w.HostExecutions.Value}");
	}

	[Fact]
	public void OneShotConsumption_SnapshotCarriesTheLatest()
	{
		var w = CreateWorld();
		var channel = w.Host.Services.GetRequiredService<EntityEventChannel>();
		var g1Consumed = new List<IReadOnlyList<EntityEventMsg>>();
		w.G1.Services.GetRequiredService<EntityEventChannel>().TrapStateReceived += list => g1Consumed.Add(list);

		// The same one-shot entity progresses (ScrapEaterProgress carries the %).
		channel.ReportTrapConsumed(EntityEventKind.ScrapEaterProgress, 30f, 40f, extra: 25);
		channel.ReportTrapConsumed(EntityEventKind.ScrapEaterProgress, 30f, 40f, extra: 50); // overwrites

		channel.SendTrapStateSnapshot(G1Id);

		Assert.True(g1Consumed.Count == 1, "the snapshot must arrive");
		Assert.True(g1Consumed[0].Count == 1, $"one consumed entity, got {g1Consumed[0].Count}");
		Assert.True(g1Consumed[0][0].Kind == EntityEventKind.ScrapEaterProgress && g1Consumed[0][0].Extra == 50,
			"the latest consumption (progress 50) is what the late joiner replays");
	}

	[Fact]
	public void OneShotConsumption_ResetClears_NewWorldStartsEmpty()
	{
		var w = CreateWorld();
		var channel = w.Host.Services.GetRequiredService<EntityEventChannel>();

		channel.ReportTrapConsumed(EntityEventKind.MineExploded, 10f, 20f, extra: 0);
		channel.ResetConsumptions(); // a new layer is generating

		var g2Consumed = new List<IReadOnlyList<EntityEventMsg>>();
		w.G2.Services.GetRequiredService<EntityEventChannel>().TrapStateReceived += list => g2Consumed.Add(list);
		channel.SendTrapStateSnapshot(G2Id);

		Assert.True(g2Consumed.Count == 0, "an empty consumption table sends nothing");
	}

	[Fact]
	public void EntitySpawned_RelayedExcludingSource()
	{
		var w = CreateWorld();
		var spawns = new List<EntitySpawnedMsg>();
		w.G2.Services.GetRequiredService<IWorldControl>().EntitySpawnedReceived += (_, msg) => spawns.Add(msg);

		w.G1.Services.GetRequiredService<IWorldControl>().SendEntitySpawned(new EntitySpawnedMsg
		{
			Id = "caveticknest",
			Position = new NetVector2Msg(7f, 8f),
		});

		// The relay is the EntitySpawnedHandler's own (source excluded) — no
		// executor participation, the creating side keeps its local copy.
		Assert.True(spawns.Count == 1, $"the other guest must get the spawn, got {spawns.Count}");
	}

	[Fact]
	public void FluidRegion_LostUnreliableRegion_HealedByTheNextAbsoluteOverwrite()
	{
		var w = CreateWorld();
		var regions = new List<FluidRegionMsg>();
		w.G1.Services.GetRequiredService<EntityEventChannel>().FluidRegionReceived += msg => regions.Add(msg);
		w.Driver.Network.SetFaults(HostId, G1Id, new LinkFaults { UnreliableDropRate = 0.5 }); // the unreliable stream loses ~half

		var channel = w.Host.Services.GetRequiredService<EntityEventChannel>();
		for (byte seq = 1; seq <= 10; seq++)
		{
			channel.SendFluidRegion(G1Id, new FluidRegionMsg { Seq = seq, OriginX = 0, OriginY = 0, Width = 4, Height = 1, Cells = [seq, 4] });
		}

		// Whatever the loss pattern, the ABSOLUTE-overwrite semantics converge:
		// the last ARRIVED region is the applied state — a lost one is healed by
		// the next (regions.Cells[0] == the value for the whole row = seq).
		Assert.True(regions.Count >= 1, "at least one region survives the loss");
		var last = regions[regions.Count - 1];
		Assert.True(last.Cells.Length == 2 && last.Cells[0] == last.Seq,
			$"the applied state is the LAST overwrite's (seq {last.Seq}, first run {(last.Cells.Length > 0 ? last.Cells[0] : -1)})");
	}

	[Fact]
	public void FluidInteraction_RelayedExcludingSource()
	{
		var w = CreateWorld();
		var drinks = new List<FluidInteractionMsg>();
		w.G2.Services.GetRequiredService<IWorldControl>().FluidInteractionReceived += (_, msg) => drinks.Add(msg);

		w.G1.Services.GetRequiredService<IWorldControl>().SendFluidInteraction(new FluidInteractionMsg
		{
			Kind = FluidInteractionMsg.KindDrink,
			X = 2,
			Y = 3,
		});

		// The relay is the FluidInteractionHandler's own (source excluded).
		Assert.True(drinks.Count == 1, $"the other guest gets the drink, got {drinks.Count}");
		Assert.True(drinks[0].X == 2 && drinks[0].Y == 3, "the cell rides through");
	}
}
