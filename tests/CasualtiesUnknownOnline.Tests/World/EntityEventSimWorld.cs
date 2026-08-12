using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The three-node entity-event world: a handshaken host + two guests on the
/// shared virtual clock, with the guests' received-event surfaces recorded
/// and the HOST EXECUTOR SHELL wired to the real channel (the production
/// TrapEffectApplier's shape: an event arrives → the per-entity one-shot
/// guard (the archive's classification) rejects duplicates, a consumption
/// records into the real TrapConsumptionRegistry, the relay is the
/// EntityEventHandler's own — the executor never relays). Shared by the
/// hand-written entity-event simulations and the phase-5 combinatorial
/// behavior tests (the archive drives both).
/// </summary>
internal sealed class EntityEventSimWorld
{
	private const ulong HostId = 1001;
	private const ulong G1Id = 2001;
	private const ulong G2Id = 3001;
	private const ulong LobbyId = 9001;

	/// <summary>Mutable execution counter — a record's captured value would never advance.</summary>
	internal sealed class ExecutionCounter
	{
		internal int Value;
	}

	private EntityEventSimWorld(
		SimulationDriver driver,
		TestNode host,
		TestNode g1,
		TestNode g2,
		List<EntityEventMsg> g1Events,
		List<EntityEventMsg> g2Events,
		ExecutionCounter hostExecutions,
		HashSet<(EntityEventKind Kind, int X, int Y)> guard,
		ExecutionCounter g1Replays,
		ExecutionCounter g2Replays)
	{
		Driver = driver;
		Host = host;
		G1 = g1;
		G2 = g2;
		G1Events = g1Events;
		G2Events = g2Events;
		HostExecutions = hostExecutions;
		Guard = guard;
		G1Replays = g1Replays;
		G2Replays = g2Replays;
	}

	internal SimulationDriver Driver { get; }

	internal TestNode Host { get; }

	internal TestNode G1 { get; }

	internal TestNode G2 { get; }

	/// <summary>The events G1 received (cumulative — the replay surface).</summary>
	internal List<EntityEventMsg> G1Events { get; }

	/// <summary>The events G2 received (cumulative — the replay surface).</summary>
	internal List<EntityEventMsg> G2Events { get; }

	/// <summary>How many times the host executor actually executed a consumption.</summary>
	internal ExecutionCounter HostExecutions { get; }

	/// <summary>The per-entity one-shot guard (the production executor's shape) — exposed for the duplicate scenarios.</summary>
	internal HashSet<(EntityEventKind Kind, int X, int Y)> Guard { get; }

	/// <summary>How many times G1's replay side actually executed (the production
	/// TrapVisualReplay's shape — relays AND snapshot consumptions both replay).</summary>
	internal ExecutionCounter G1Replays { get; }

	/// <summary>How many times G2's replay side actually executed.</summary>
	internal ExecutionCounter G2Replays { get; }

	/// <summary>Fired after the host executor ACTUALLY executed a consumption
	/// (the per-entity guard passed) — the cross-domain shells hook here to
	/// emit an explosion's side-effect reports (crater/drops/building damage).</summary>
	internal event Action<EntityEventMsg>? HostExecuted;

	/// <summary>The host's consumption registry (the late-joiner snapshot's fact source).</summary>
	internal TrapConsumptionRegistry Registry => Host.Services.GetRequiredService<TrapConsumptionRegistry>();

	/// <summary>The host's event channel (snapshot send/reset surface).</summary>
	internal EntityEventChannel HostChannel => Host.Services.GetRequiredService<EntityEventChannel>();

	internal static EntityEventSimWorld Create()
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

		var registry = host.Services.GetRequiredService<TrapConsumptionRegistry>();
		var guard = new HashSet<(EntityEventKind Kind, int X, int Y)>(); // the per-entity one-shot guard
		var hostExecutions = new ExecutionCounter();
		var g1Replays = new ExecutionCounter();
		var g2Replays = new ExecutionCounter();
		var world = new EntityEventSimWorld(driver, host, g1, g2, g1Events, g2Events, hostExecutions, guard, g1Replays, g2Replays);

		host.Services.GetRequiredService<IWorldControl>().EntityEventReceived += (_, msg) =>
		{
			var key = ((int)Math.Floor(msg.Position.X), (int)Math.Floor(msg.Position.Y));
			if (EntityEventProfiles.IsOneShotConsumption(msg.Kind) && !guard.Add((msg.Kind, key.Item1, key.Item2)))
			{
				return; // duplicate — the production per-entity guard drops the re-execution
			}

			hostExecutions.Value++; // a consumption actually executed
			registry.Report(msg.Kind, msg.Position.X, msg.Position.Y, msg.Extra);
			world.HostExecuted?.Invoke(msg);
		};

		// The guests' replay shells (the production TrapVisualReplay's shape +
		// the snapshot-consumption step): relays replay; the late-joiner
		// snapshot consumes every entry. The per-entity guard makes both
		// idempotent — a duplicate entry never re-executes.
		AttachReplayShell(g1, g1Replays);
		AttachReplayShell(g2, g2Replays);

		return world;
	}

	private static void AttachReplayShell(TestNode guest, ExecutionCounter replays)
	{
		var world = guest.Services.GetRequiredService<IWorldControl>();
		var guard = new HashSet<(EntityEventKind Kind, int X, int Y)>();
		world.EntityEventReceived += (_, msg) => ReplayOnce(msg, guard, replays);
		world.TrapStateReceived += consumed =>
		{
			foreach (var msg in consumed)
			{
				ReplayOnce(msg, guard, replays);
			}
		};
	}

	private static void ReplayOnce(EntityEventMsg msg, HashSet<(EntityEventKind Kind, int X, int Y)> guard, ExecutionCounter replays)
	{
		var key = ((int)Math.Floor(msg.Position.X), (int)Math.Floor(msg.Position.Y));
		if (EntityEventProfiles.IsOneShotConsumption(msg.Kind) && !guard.Add((msg.Kind, key.Item1, key.Item2)))
		{
			return; // duplicate — the receiving-side replay guard drops it
		}

		replays.Value++;
	}

	/// <summary>One trigger report from the given node (the game-side SendEntityEvent surface).</summary>
	internal void Trigger(TestNode node, EntityEventKind kind, float x, float y, byte extra = 0) =>
		node.Services.GetRequiredService<IWorldControl>().SendEntityEvent(new EntityEventMsg
		{
			Kind = kind,
			Position = new NetVector2Msg(x, y),
			Extra = extra,
		});
}
