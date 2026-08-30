using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.Protocol.Versioning;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The enemy-sync stream (EnemySyncService): the host publishes its simulated
/// enemies, the world-entry fan-out delivers the full snapshot (ids + positions
/// for binding), and the 20 Hz broadcast reaches in-world guests — with the
/// unreliable-stream seq gate dropping stale/duplicate batches. Locked through
/// the real handlers over the fake network.
/// </summary>
public class EnemySyncServiceTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static EnemyEntity Enemy(uint counter, float x, float y, float health = 100f) =>
		new(new NetworkEntityId(1, counter, 0))
		{
			Position = new NetVector2(x, y),
			Velocity = new NetVector2(0f, 0f),
			Rotation = 0f,
			Health = health,
			Stunned = false,
		};

	private static ProtocolFrame EnemyStreamFrame(uint seq, params WireEnemyStreamState[] states) =>
		new()
		{
			Kind = EnvelopeKind.StateStream,
			StateStream = new StateStreamEnvelope
			{
				Header = new EnvelopeHeader
				{
					ProtocolVersion = ProtocolConstants.EnvelopeVersion,
					PayloadType = WirePayloadType.EnemyStateStream,
				},
				Stream = new WireStateStream
				{
					Seq = seq,
					EnemyStates = [.. states],
				},
			},
		};

	[Fact]
	public void WorldEntry_ReceivesFullEnemySnapshot()
	{
		using var w = ItemSimWorld.Create();
		w.Host.Services.GetRequiredService<EnemySyncService>()
			.PublishEnemyStates([Enemy(0, 10f, 20f), Enemy(1, 30f, 40f)]);

		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		var received = 0;
		g1Enemies.EnemySnapshotReceived += () => received++;

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		Assert.True(received > 0, "the world-entry enemy snapshot must fire the received event");
		Assert.Equal(2, g1Enemies.Enemies.Count());
		Assert.NotNull(g1Enemies.GetEnemy(new NetworkEntityId(1, 0, 0)));
		Assert.Equal(new NetVector2(30f, 40f), g1Enemies.GetEnemy(new NetworkEntityId(1, 1, 0))!.Position);
	}
	[Fact]
	public void WorldEntry_RuntimeSpawnFacts_RideTheSnapshot()
	{
		using var w = ItemSimWorld.Create();
		var hostEnemies = w.Host.Services.GetRequiredService<EnemySyncService>();
		hostEnemies.PublishEnemyStates(
		[
			Enemy(0, 10f, 20f),
			new EnemyEntity(new NetworkEntityId(1, 1, 0))
			{
				Position = new NetVector2(30f, 40f),
				Velocity = new NetVector2(1f, 2f),
				Rotation = 33f,
				Health = 15f,
				PrefabId = "cavetick",
				RuntimeSpawned = true,
			},
		]);

		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		var received = 0;
		g1Enemies.EnemySnapshotReceived += () => received++;
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		Assert.True(received > 0, "the snapshot must arrive");
		var spawn = Assert.Single(g1Enemies.RuntimeSpawns);
		Assert.Equal(new NetworkEntityId(1, 1, 0), spawn.Id.ToNetworkEntityId());
		Assert.Equal("cavetick", spawn.PrefabId);
		Assert.Equal(new NetVector2(30f, 40f), spawn.Position.ToNetVector2());
		Assert.Equal(33f, spawn.Rotation);
	}


	[Fact]
	public void WorldEntry_NoEnemies_SendsNothing()
	{
		using var w = ItemSimWorld.Create();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		var received = 0;
		g1Enemies.EnemySnapshotReceived += () => received++;

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		Assert.Empty(g1Enemies.Enemies);
		Assert.True(received == 0, "an empty enemy table must not fire the received event");
	}

	[Fact]
	public void PeriodicBroadcast_ReachesInWorldGuest()
	{
		using var w = ItemSimWorld.Create();
		w.Host.Services.GetRequiredService<EnemySyncService>()
			.PublishEnemyStates([Enemy(0, 5f, 5f)]);

		// World entry delivers the snapshot; then the 20 Hz broadcast follows.
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		var received = 0;
		w.G1.Services.GetRequiredService<EnemySyncService>().EnemyStateReceived += () => received++;
		w.Driver.Tick(60); // past the 50 ms throttle

		Assert.True(received > 0, "the 20 Hz broadcast must reach an in-world guest");
	}

	[Fact]
	public void StaleAndDuplicateSequences_Dropped_NewerPass()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var enemies = guest.Services.GetRequiredService<IEnemySyncControl>();
		var sender = host.Services.GetRequiredService<PacketSender>();

		var dummy = Enemy(0, 0f, 0f).ToWireEnemyStreamState();
		sender.Send(GuestId, NetMsg.KernelEnvelope, EnemyStreamFrame(seq: 1, dummy), reliable: false);
		Assert.Equal(1u, enemies.LastEnemyStateSeq);

		sender.Send(GuestId, NetMsg.KernelEnvelope, EnemyStreamFrame(seq: 1, dummy), reliable: false); // duplicate
		sender.Send(GuestId, NetMsg.KernelEnvelope, EnemyStreamFrame(seq: 0, dummy), reliable: false); // stale (reordered)
		Assert.Equal(1u, enemies.LastEnemyStateSeq);

		sender.Send(GuestId, NetMsg.KernelEnvelope, EnemyStreamFrame(seq: 2, dummy), reliable: false);
		Assert.Equal(2u, enemies.LastEnemyStateSeq);
	}

	[Fact]
	public void EnemySnapshot_Overwrites_PreviousBuffer()
	{
		using var w = ItemSimWorld.Create();
		var hostEnemies = w.Host.Services.GetRequiredService<EnemySyncService>();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();

		hostEnemies.PublishEnemyStates([Enemy(0, 1f, 1f), Enemy(1, 2f, 2f)]);
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);
		Assert.Equal(2, g1Enemies.Enemies.Count());

		// The host's table shrinks to one — the next snapshot overwrites, not appends.
		hostEnemies.PublishEnemyStates([Enemy(1, 2f, 2f)]);
		w.Driver.Tick(60);

		Assert.Single(g1Enemies.Enemies);
		Assert.NotNull(g1Enemies.GetEnemy(new NetworkEntityId(1, 1, 0)));
		Assert.Null(g1Enemies.GetEnemy(new NetworkEntityId(1, 0, 0)));
	}

	[Fact]
	public void StateBatch_MissingId_DoesNotRemoveExistingBuffer()
	{
		using var w = ItemSimWorld.Create();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		var control = (IEnemySyncControl)g1Enemies;

		control.ApplyEnemyStream(new WireStateStream
		{
			EnemyStates = [Enemy(0, 1f, 1f).ToWireEnemyStreamState()],
		});
		control.ApplyEnemyStream(new WireStateStream { EnemyStates = [] });

		Assert.NotNull(g1Enemies.GetEnemy(new NetworkEntityId(1, 0, 0)));
	}

	[Fact]
	public void StaleStream_CannotOverwriteNewerKernelHealth()
	{
		using var w = ItemSimWorld.Create();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		var control = (IEnemySyncControl)g1Enemies;
		var id = new NetworkEntityId(1, 9, 0);

		control.ApplyEnemyStream(new WireStateStream
		{
			EnemyStates = [Enemy(9, 1f, 1f, health: 50f).ToWireEnemyStreamState()],
		});

		var authority = w.G1.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.True(authority.TryUpsertEnemy(
			w.G1.SteamId,
			new EnemyState(new EntityId(1, 9, 0), "cavetick", 10f, false, false),
			out var batch,
			out _));
		Assert.True(batch!.GlobalRevision > 0);

		// A late/out-of-order 20 Hz batch from before the kernel health event
		// must not roll the terminal health back, while continuous position
		// fields still converge.
		control.ApplyEnemyStream(new WireStateStream
		{
			BaseGlobalRevision = batch.GlobalRevision - 1,
			EnemyStates = [Enemy(9, 2f, 2f, health: 90f).ToWireEnemyStreamState()],
		});

		var enemy = g1Enemies.GetEnemy(id);
		Assert.NotNull(enemy);
		Assert.Equal(50f, enemy!.Health);
		Assert.Equal(new NetVector2(2f, 2f), enemy.Position);

		// The current stream refreshes the terminal health from the kernel
		// event, then a later stale packet still cannot roll it back.
		control.ApplyEnemyStream(new WireStateStream
		{
			BaseGlobalRevision = batch.GlobalRevision,
			EnemyStates = [Enemy(9, 3f, 3f, health: 10f).ToWireEnemyStreamState()],
		});
		var currentEnemy = g1Enemies.GetEnemy(id);
		Assert.NotNull(currentEnemy);
		Assert.Equal(10f, currentEnemy!.Health);

		control.ApplyEnemyStream(new WireStateStream
		{
			BaseGlobalRevision = batch.GlobalRevision - 1,
			EnemyStates = [Enemy(9, 4f, 4f, health: 90f).ToWireEnemyStreamState()],
		});
		var finalEnemy = g1Enemies.GetEnemy(id);
		Assert.NotNull(finalEnemy);
		Assert.Equal(10f, finalEnemy!.Health);
		Assert.Equal(new NetVector2(4f, 4f), finalEnemy.Position);
	}

	[Fact]
	public void RemovalMessage_RemovesEnemyAndRaisesEvent()
	{
		using var w = ItemSimWorld.Create();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		var control = (IEnemySyncControl)g1Enemies;
		var id = new NetworkEntityId(1, 2, 0);
		var removed = new List<NetworkEntityId>();
		g1Enemies.EnemyRemovedReceived += removedId => removed.Add(removedId);

		control.ApplyEnemyStream(new WireStateStream
		{
			EnemyStates = [Enemy(2, 3f, 4f).ToWireEnemyStreamState()],
		});
		control.ApplyEnemyRemoved(new EnemyRemovedMsg { Id = id.ToNetworkEntityIdMsg() });

		Assert.Null(g1Enemies.GetEnemy(id));
		Assert.Equal(id, Assert.Single(removed));
	}

	[Fact]
	public void RemovedEnemy_ConvergesEvenWhenStateBatchDrops()
	{
		using var w = ItemSimWorld.Create();
		var hostEnemies = w.Host.Services.GetRequiredService<EnemySyncService>();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();

		hostEnemies.PublishEnemyStates([Enemy(0, 1f, 1f), Enemy(1, 2f, 2f)]);
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);
		Assert.Equal(2, g1Enemies.Enemies.Count());

		// Drop every unreliable state batch after the initial snapshot; the
		// explicit removal is reliable and must still converge the guest.
		w.Driver.Network.SetFaults(
			w.Host.SteamId,
			w.G1.SteamId,
			new LinkFaults { UnreliableDropRate = 1.0 });
		hostEnemies.PublishEnemyStates([Enemy(1, 2f, 2f)]);
		w.Driver.Tick(60);

		Assert.Single(g1Enemies.Enemies);
		Assert.NotNull(g1Enemies.GetEnemy(new NetworkEntityId(1, 1, 0)));
		Assert.Null(g1Enemies.GetEnemy(new NetworkEntityId(1, 0, 0)));
	}

	[Fact]
	public void RemovedEnemy_NotResurrectedByLateStateBatch()
	{
		using var w = ItemSimWorld.Create();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		var control = (IEnemySyncControl)g1Enemies;
		var id = new NetworkEntityId(1, 5, 0);

		control.ApplyEnemyStream(new WireStateStream
		{
			EnemyStates = [Enemy(5, 1f, 1f).ToWireEnemyStreamState()],
		});
		control.ApplyEnemyRemoved(new EnemyRemovedMsg { Id = id.ToNetworkEntityIdMsg() });
		control.ApplyEnemyStream(new WireStateStream
		{
			EnemyStates = [Enemy(5, 2f, 2f).ToWireEnemyStreamState()],
		});

		Assert.Null(g1Enemies.GetEnemy(id));
	}

	[Fact]
	public void RemovedEnemy_NotResurrectedByFullSnapshot()
	{
		using var w = ItemSimWorld.Create();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		var control = (IEnemySyncControl)g1Enemies;
		var id = new NetworkEntityId(1, 6, 0);

		control.ApplyEnemyStream(new WireStateStream
		{
			EnemyStates = [Enemy(6, 1f, 1f).ToWireEnemyStreamState()],
		});
		control.ApplyEnemyRemoved(new EnemyRemovedMsg { Id = id.ToNetworkEntityIdMsg() });
		control.ApplyEnemySnapshot(new EnemySnapshotMsg
		{
			Enemies = [Enemy(6, 3f, 3f).ToEnemyStateMsg()],
		});

		Assert.Null(g1Enemies.GetEnemy(id));
	}

	[Fact]
	public void HostSendEnemySnapshot_ProjectsKernelTerminalFacts()
	{
		using var w = ItemSimWorld.Create();
		var hostEnemies = w.Host.Services.GetRequiredService<EnemySyncService>();
		var authority = w.Host.Services.GetRequiredService<ItemKernelAuthority>();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		var received = 0;
		g1Enemies.EnemySnapshotReceived += () => received++;
		var id = new NetworkEntityId(1, 0, 0);

		hostEnemies.PublishEnemyStates(
		[
			new EnemyEntity(id)
			{
				Position = new NetVector2(10f, 20f),
				Health = 80f,
				Stunned = false,
				PrefabId = "cavetick",
				RuntimeSpawned = false,
			},
		]);

		Assert.True(authority.TryUpsertEnemy(
			w.Host.SteamId,
			new EnemyState(new EntityId(1, 0, 0), "cavetick", 10f, false, true),
			out _,
			out var rejection), rejection?.Message);

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		Assert.True(received > 0, "the world-entry enemy snapshot must arrive");
		var enemy = g1Enemies.GetEnemy(id);
		Assert.NotNull(enemy);
		Assert.Equal(10f, enemy!.Health);
		Assert.True(enemy.Stunned);
	}

	[Fact]
	public void GuestApplyEnemySnapshot_ProjectsKernelTerminalFacts()
	{
		using var w = ItemSimWorld.Create();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		var control = (IEnemySyncControl)g1Enemies;
		var authority = w.G1.Services.GetRequiredService<ItemKernelAuthority>();
		var id = new NetworkEntityId(1, 7, 0);

		Assert.True(authority.TryUpsertEnemy(
			w.G1.SteamId,
			new EnemyState(new EntityId(1, 7, 0), "cavetick", 10f, true, true),
			out _,
			out var rejection), rejection?.Message);

		control.ApplyEnemySnapshot(new EnemySnapshotMsg
		{
			Enemies =
			[
				new EnemyEntity(id)
				{
					Position = new NetVector2(3f, 4f),
					Health = 90f,
					Stunned = false,
					RuntimeSpawned = false,
				}.ToEnemyStateMsg(),
			],
			RuntimeSpawns = [],
		});

		var enemy = g1Enemies.GetEnemy(id);
		Assert.NotNull(enemy);
		Assert.Equal(10f, enemy!.Health);
		Assert.True(enemy.Stunned);
		Assert.True(enemy.RuntimeSpawned);
		Assert.Equal("cavetick", enemy.PrefabId);
	}
}
