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
/// behavior tests (the archive drives both); the phase-A1 replay runner
/// drives it too (the event/snapshot/fluid actions and the replayed/executed/
/// fluid assertions — the replay files' entity/fluid bug fossils).
/// </summary>
internal sealed class EntityEventSimWorld : IDisposable
{
	private const ulong HostId = 1001;
	private const ulong G1Id = 2001;
	private const ulong G2Id = 3001;
	private const ulong LobbyId = 9001;

	/// <summary>The applied-fluid grid is unbounded in the simulation (the world
	/// grid's clamp semantics are the pure codec's, locked by FluidRleCodecTests).</summary>
	private const int FluidGridSize = 10000;

	/// <summary>Mutable execution counter — a record's captured value would never advance.</summary>
	internal sealed class ExecutionCounter
	{
		internal int Value;
	}

	/// <summary>A guest's observed surface: the events it received, its replay
	/// executions (total + per kind), the fluid regions it applied (the
	/// ABSOLUTE-overwrite grid) and the trap-state snapshots it consumed.</summary>
	private sealed class NodeSurface
	{
		internal List<EntityEventMsg> Events = [];
		internal ExecutionCounter Replays = new();
		internal Dictionary<EntityEventKind, int> ReplaysByKind = [];
		internal Dictionary<(int X, int Y), byte> FluidCells = [];
		internal ExecutionCounter FluidRegions = new();
		internal List<IReadOnlyList<EntityEventMsg>> Snapshots = [];
	}

	private readonly NodeSurface _g1Surface = new();
	private readonly NodeSurface _g2Surface = new();

	private EntityEventSimWorld(
		SimulationDriver driver,
		TestNode host,
		TestNode g1,
		TestNode g2,
		ExecutionCounter hostExecutions,
		Dictionary<EntityEventKind, int> hostExecutionsByKind,
		HashSet<(EntityEventKind Kind, int X, int Y)> guard)
	{
		Driver = driver;
		Host = host;
		G1 = g1;
		G2 = g2;
		HostExecutions = hostExecutions;
		HostExecutionsByKind = hostExecutionsByKind;
		Guard = guard;
	}

	internal SimulationDriver Driver { get; }

	internal TestNode Host { get; }

	internal TestNode G1 { get; }

	internal TestNode G2 { get; }

	/// <summary>The events G1 received (cumulative — the replay surface).</summary>
	internal List<EntityEventMsg> G1Events => _g1Surface.Events;

	/// <summary>The events G2 received (cumulative — the replay surface).</summary>
	internal List<EntityEventMsg> G2Events => _g2Surface.Events;

	/// <summary>How many times the host executor actually executed a consumption.</summary>
	internal ExecutionCounter HostExecutions { get; }

	/// <summary>The host executor's executions per kind (the executed-assertion surface).</summary>
	internal Dictionary<EntityEventKind, int> HostExecutionsByKind { get; }

	/// <summary>The per-entity one-shot guard (the production executor's shape) — exposed for the duplicate scenarios.</summary>
	internal HashSet<(EntityEventKind Kind, int X, int Y)> Guard { get; }

	/// <summary>How many times G1's replay side actually executed (the production
	/// TrapVisualReplay's shape — relays AND snapshot consumptions both replay).</summary>
	internal ExecutionCounter G1Replays => _g1Surface.Replays;

	/// <summary>How many times G2's replay side actually executed.</summary>
	internal ExecutionCounter G2Replays => _g2Surface.Replays;

	/// <summary>Fired after the host executor ACTUALLY executed a consumption
	/// (the per-entity guard passed) — the cross-domain shells hook here to
	/// emit an explosion's side-effect reports (crater/drops/building damage).</summary>
	internal event Action<EntityEventMsg>? HostExecuted;

	/// <summary>The host's consumption registry (the late-joiner snapshot's fact source).</summary>
	internal TrapConsumptionRegistry Registry => Host.Services.GetRequiredService<TrapConsumptionRegistry>();

	/// <summary>The host's event channel (snapshot send/reset surface).</summary>
	internal EntityEventChannel HostChannel => Host.Services.GetRequiredService<EntityEventChannel>();

	public void Dispose()
	{
		Host.Dispose();
		G1.Dispose();
		G2.Dispose();
	}

	/// <summary>Resolve a replay-file node alias ("host"/"g1"/"g2").</summary>
	internal TestNode Node(string alias) => alias switch
	{
		"host" => Host,
		"g1" => G1,
		"g2" => G2,
		_ => throw new ArgumentException($"unknown node alias '{alias}' (host/g1/g2)"),
	};

	/// <summary>How many times the node's replay side executed one kind (guards passed).</summary>
	internal int ReplaysOf(TestNode node, EntityEventKind kind) => CountOf(Surface(node).ReplaysByKind, kind);

	/// <summary>How many times the host executor executed one kind (guards passed).</summary>
	internal int HostExecutionsOf(EntityEventKind kind) => CountOf(HostExecutionsByKind, kind);

	private static int CountOf(Dictionary<EntityEventKind, int> byKind, EntityEventKind kind) =>
		byKind.TryGetValue(kind, out var count) ? count : 0;

	/// <summary>The node's applied-fluid value at a cell — the LAST arrived
	/// region's absolute overwrite (0 = never covered / cleared).</summary>
	internal byte FluidCell(TestNode node, int x, int y) => Surface(node).FluidCells.TryGetValue((x, y), out var value) ? value : (byte)0;

	/// <summary>How many fluid regions the node has applied (the SimTrace "Committed(n)" surface).</summary>
	internal int FluidRegions(TestNode node) => Surface(node).FluidRegions.Value;

	/// <summary>The trap-state snapshots the node has received (each snapshot's
	/// entries = the late-joiner consumptions — the SimTrace "Committed(n)" surface).</summary>
	internal List<IReadOnlyList<EntityEventMsg>> Snapshots(TestNode node) => Surface(node).Snapshots;

	private NodeSurface Surface(TestNode node)
	{
		if (node == G1)
		{
			return _g1Surface;
		}

		if (node == G2)
		{
			return _g2Surface;
		}

		throw new ArgumentException("only g1/g2 surfaces are recorded", nameof(node));
	}

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

		var registry = host.Services.GetRequiredService<TrapConsumptionRegistry>();
		var guard = new HashSet<(EntityEventKind Kind, int X, int Y)>(); // the per-entity one-shot guard
		var hostExecutions = new ExecutionCounter();
		var hostExecutionsByKind = new Dictionary<EntityEventKind, int>();
		var world = new EntityEventSimWorld(driver, host, g1, g2, hostExecutions, hostExecutionsByKind, guard);

		g1.Services.GetRequiredService<IWorldControl>().EntityEventReceived += (_, msg) => world._g1Surface.Events.Add(msg);
		g2.Services.GetRequiredService<IWorldControl>().EntityEventReceived += (_, msg) => world._g2Surface.Events.Add(msg);

		host.Services.GetRequiredService<IWorldControl>().EntityEventReceived += (sender, msg) =>
		{
			var key = ((int)Math.Floor(msg.Position.X), (int)Math.Floor(msg.Position.Y));
			if (!EntityEventProfiles.IsOneShotConsumption(msg.Kind) || guard.Add((msg.Kind, key.Item1, key.Item2)))
			{
				hostExecutions.Value++; // a consumption actually executed
				hostExecutionsByKind[msg.Kind] = CountOf(hostExecutionsByKind, msg.Kind) + 1;
				if (EntityEventProfiles.IsOneShotConsumption(msg.Kind))
				{
					registry.Report(msg.Kind, msg.Position.X, msg.Position.Y, msg.Extra);
				}

				world.HostExecuted?.Invoke(msg);
			}

			// The production EntityEventSync relays unconditionally after the
			// host apply (the receiving side's replay guard drops duplicates).
			world.HostChannel.BroadcastEntityEvent(sender, msg);
		};

		// The production EntitySpawnSync shell: the host creates its copy and
		// is the single relay owner (the handler only surfaces the message).
		host.Services.GetRequiredService<IWorldControl>().EntitySpawnedReceived += (_, msg) =>
			world.HostChannel.SendEntitySpawned(msg);

		// The guests' replay shells (the production TrapVisualReplay's shape +
		// the snapshot-consumption step): relays replay; the late-joiner
		// snapshot consumes every entry. The per-entity guard makes both
		// idempotent — a duplicate entry never re-executes.
		AttachReplayShell(g1, world._g1Surface);
		AttachReplayShell(g2, world._g2Surface);

		// The guests' applied-fluid surface (the production FluidWorldSync →
		// FluidRegionApplication shape): every arrived region ABSOLUTELY
		// overwrites its rectangle through the real RLE decoder — a decoder
		// regression (81dd26a's mid-region zero run) breaks the replay
		// assertions instead of silently replaying the old bug.
		AttachFluidSurface(g1, world._g1Surface);
		AttachFluidSurface(g2, world._g2Surface);

		return world;
	}

	private static void AttachReplayShell(TestNode guest, NodeSurface surface)
	{
		var world = guest.Services.GetRequiredService<IWorldControl>();
		var guard = new HashSet<(EntityEventKind Kind, int X, int Y)>();
		world.EntityEventReceived += (_, msg) => ReplayOnce(msg, guard, surface);
		world.TrapStateReceived += consumed =>
		{
			surface.Snapshots.Add(consumed);
			foreach (var msg in consumed)
			{
				ReplayOnce(msg, guard, surface);
			}
		};
	}

	private static void AttachFluidSurface(TestNode guest, NodeSurface surface)
	{
		guest.Services.GetRequiredService<EntityEventChannel>().FluidRegionReceived += msg =>
		{
			surface.FluidRegions.Value++;
			FluidRleCodec.Decode(
				msg.Cells, msg.Width, msg.Height, msg.OriginX, msg.OriginY, FluidGridSize, FluidGridSize,
				(x, y, value) => surface.FluidCells[(x, y)] = value);
		};
	}

	private static void ReplayOnce(EntityEventMsg msg, HashSet<(EntityEventKind Kind, int X, int Y)> guard, NodeSurface surface)
	{
		var key = ((int)Math.Floor(msg.Position.X), (int)Math.Floor(msg.Position.Y));
		if (EntityEventProfiles.IsOneShotConsumption(msg.Kind) && !guard.Add((msg.Kind, key.Item1, key.Item2)))
		{
			return; // duplicate — the receiving-side replay guard drops it
		}

		surface.Replays.Value++;
		surface.ReplaysByKind[msg.Kind] = CountOf(surface.ReplaysByKind, msg.Kind) + 1;
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
