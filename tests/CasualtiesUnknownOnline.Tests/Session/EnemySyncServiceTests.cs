using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
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

		sender.Send(GuestId, NetMsg.EnemyState, new EnemyStateBatchMsg { Seq = 1 }, reliable: false);
		Assert.Equal(1u, enemies.LastEnemyStateSeq);

		sender.Send(GuestId, NetMsg.EnemyState, new EnemyStateBatchMsg { Seq = 1 }, reliable: false); // duplicate
		sender.Send(GuestId, NetMsg.EnemyState, new EnemyStateBatchMsg { Seq = 0 }, reliable: false); // stale (reordered)
		Assert.Equal(1u, enemies.LastEnemyStateSeq);

		sender.Send(GuestId, NetMsg.EnemyState, new EnemyStateBatchMsg { Seq = 2 }, reliable: false);
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
}
