using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The enemy roster snapshot half (sync-coverage audit row N1): the world-entry /
/// reconnect <c>EnemySnapshot</c> is the absolute roster, the in-session repair
/// re-sends it, and an EMPTY roster is authoritative too — a member destroys its
/// surplus copies instead of keeping a divergent local set. The snapshot's match
/// hint travels beside the live position but is not an identity. A roster of
/// another world generation is refused before anything is applied.
/// </summary>
[Trait("Category", "Integration")]
public class EnemySnapshotRecoveryTests
{
	private static EnemyEntity Enemy(uint counter, float x, float y) =>
		new(new NetworkEntityId(1, counter, 0))
		{
			Position = new NetVector2(x, y),
			Velocity = new NetVector2(0f, 0f),
			Rotation = 0f,
			Health = 100f,
			PrefabId = "cavetick",
		};

	/// <summary>An enemy whose live position has moved away from the hint the host recorded when it bound it.</summary>
	private static EnemyEntity Hinted(NetworkEntityId id, NetVector2 live, NetVector2 hint) =>
		new(id)
		{
			Position = live,
			SpawnPosition = hint,
			Velocity = new NetVector2(0f, 0f),
			Health = 100f,
			PrefabId = "cavetick",
		};

	[Fact]
	public void EnemiesPublishedAfterTheEntryEdge_StillReachAStayingMember()
	{
		// g1 enters while the host's enemy table is still empty: the entry
		// fan-out's snapshot is empty, and no further InWorld edge is coming while
		// it stays. The in-session repair group is the only path that can still
		// carry the rows it must adopt (N1).
		using var w = ItemSimWorld.Create();
		var hostEnemies = w.Host.Services.GetRequiredService<EnemySyncService>();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		var snapshots = 0;
		g1Enemies.EnemySnapshotReceived += () => snapshots++;

		hostEnemies.PublishEnemyStates([Enemy(3, -13f, 466.8f)]);

		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(50);

		Assert.True(snapshots == 1, $"the in-session repair must deliver the enemy roster, got {snapshots}");
		var bound = g1Enemies.GetEnemy(new NetworkEntityId(1, 3, 0));
		Assert.True(bound is not null, "the staying member must learn the host's enemy row");
		Assert.Equal(-13f, bound!.Position.X);
		Assert.Equal(466.8f, bound.Position.Y);
	}

	[Fact]
	public void EmptyEnemyTable_TheRepairSendsTheAuthoritativeEmptyRoster()
	{
		// An empty host table is a FACT, not a no-op: the member's own generated
		// enemies are a different world. The snapshot must travel so the member can
		// destroy its surplus copies; suppressing it kept the divergent set forever.
		using var w = ItemSimWorld.Create();
		var frames = new List<NetMsg>();
		w.G1.Transport.MessageReceived += (_, frame) => frames.Add((NetMsg)frame[0]);
		var received = 0;
		w.G1.Services.GetRequiredService<EnemySyncService>().EnemySnapshotReceived += () => received++;

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(50);

		Assert.Contains(NetMsg.EnemySnapshot, frames);
		Assert.True(received > 0, "the empty roster must fire the received event so the adapter can reconcile");
		Assert.Empty(w.G1.Services.GetRequiredService<EnemySyncService>().Enemies);
	}

	[Fact]
	public void SnapshotCarriesTheMatchHint_BesideTheLivePosition()
	{
		using var w = ItemSimWorld.Create();
		var hostEnemies = w.Host.Services.GetRequiredService<EnemySyncService>();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		var id = new NetworkEntityId(1, 4, 0);

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		hostEnemies.PublishEnemyStates([Hinted(id, live: new NetVector2(40f, 55f), hint: new NetVector2(10f, 20f))]);
		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(50);

		var bound = g1Enemies.GetEnemy(id);
		Assert.True(bound is not null, "the repair snapshot must deliver the row");
		Assert.Equal(40f, bound!.Position.X);
		Assert.Equal(55f, bound.Position.Y);
		Assert.Equal(10f, bound.SpawnPosition.X);
		Assert.Equal(20f, bound.SpawnPosition.Y);
	}

	[Fact]
	public void RepairSnapshot_RepeatsTheSameRow_AndRefreshesTheLivePosition()
	{
		using var w = ItemSimWorld.Create();
		var hostEnemies = w.Host.Services.GetRequiredService<EnemySyncService>();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		var repair = w.Host.Services.GetRequiredService<WorldEntryFanout>();
		var id = new NetworkEntityId(1, 4, 0);

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		hostEnemies.PublishEnemyStates([Hinted(id, live: new NetVector2(11f, 21f), hint: new NetVector2(10f, 20f))]);
		repair.SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(50);

		hostEnemies.PublishEnemyStates([Hinted(id, live: new NetVector2(40f, 55f), hint: new NetVector2(10f, 20f))]);
		repair.SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(50);

		var bound = g1Enemies.GetEnemy(id);
		Assert.True(bound is not null, "the id stays in the buffer across repairs");
		Assert.Equal(40f, bound!.Position.X);
		Assert.Equal(10f, bound.SpawnPosition.X);
		Assert.True(g1Enemies.Enemies.Count() == 1, $"a repeat snapshot never duplicates the id, got {g1Enemies.Enemies.Count()}");
	}

	[Fact]
	public void StreamFrames_DoNotForgetTheMatchHint()
	{
		// The 20 Hz stream is update-only and its payload has no hint, so a stream
		// frame that rebuilt the buffered entry from scratch would silently zero it
		// — the next adoption would then replace rather than reuse the local copy.
		using var w = ItemSimWorld.Create();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		var control = (IEnemySyncControl)g1Enemies;
		var id = new NetworkEntityId(1, 5, 0);

		control.ApplyEnemySnapshot(0, new EnemySnapshotMsg
		{
			Enemies = [Hinted(id, live: new NetVector2(1f, 2f), hint: new NetVector2(10f, 20f)).ToEnemyStateMsg()],
		});
		control.ApplyEnemyStream(new WireStateStream
		{
			EnemyStates = [Enemy(5, 30f, 40f).ToWireEnemyStreamState()],
		});

		var bound = g1Enemies.GetEnemy(id);
		Assert.True(bound is not null, "the stream refreshes the entry, it does not drop it");
		Assert.Equal(30f, bound!.Position.X);
		Assert.Equal(10f, bound.SpawnPosition.X);
		Assert.Equal(20f, bound.SpawnPosition.Y);
	}

	[Fact]
	public void SnapshotOfAnotherGeneration_IsRefused()
	{
		// A roster names a layer's rows; after a descent the same ids never come
		// back. The stamp is the kernel run baseline both sides already follow.
		using var w = ItemSimWorld.Create();
		var authority = w.G1.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.True(authority.TryStartRun(
			w.G1.SteamId,
			new RunState(1UL, [1, 2, 3, 4, 5, 6, 7, 8], 0, 0, 0, false, null, 4),
			out _,
			out var rejection), rejection?.Message);

		var epoch = authority.CreateCheckpoint().RunEpoch.Value;
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		var control = (IEnemySyncControl)g1Enemies;
		var id = new NetworkEntityId(1, 6, 0);

		control.ApplyEnemySnapshot(0, new EnemySnapshotMsg
		{
			Enemies = [Enemy(6, 1f, 2f).ToEnemyStateMsg()],
			Generation = new WorldGenerationMsg { RunEpoch = epoch, LayerIndex = 5 },
		});
		Assert.Null(g1Enemies.GetEnemy(id));

		control.ApplyEnemySnapshot(0, new EnemySnapshotMsg
		{
			Enemies = [Enemy(6, 1f, 2f).ToEnemyStateMsg()],
			Generation = new WorldGenerationMsg { RunEpoch = epoch, LayerIndex = 4 },
		});
		Assert.NotNull(g1Enemies.GetEnemy(id));
	}

	[Fact]
	public void RosterReset_ClearsTheGuestBufferAndRaisesTheAdapterEvent()
	{
		using var w = ItemSimWorld.Create();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		var control = (IEnemySyncControl)g1Enemies;
		var id = new NetworkEntityId(1, 7, 0);
		var resets = 0;
		g1Enemies.EnemyRosterReset += () => resets++;

		control.ApplyEnemySnapshot(0, new EnemySnapshotMsg
		{
			Enemies = [Enemy(7, 1f, 2f).ToEnemyStateMsg()],
		});
		Assert.NotNull(g1Enemies.GetEnemy(id));

		var authority = w.G1.Services.GetRequiredService<ItemKernelAuthority>();
		var current = authority.CreateCheckpoint();
		var batch = new CommittedBatch(
			new OperationId(1),
			current.GlobalRevision + 1,
			new ActorId(w.Host.SteamId),
			AuthorityKind.HostOnly,
			current.RunEpoch,
			[],
			[new EnemiesResetEvent()]);
		Assert.True(authority.Apply(batch).Success);

		Assert.Empty(g1Enemies.Enemies);
		Assert.Equal(1, resets);
	}

	[Fact]
	public void StreamFrames_PreserveTheSnapshotOnlyFields()
	{
		// A deferred adoption applies frames after the snapshot; if a 20 Hz rebuild
		// dropped the prefab/key, every streamed row would look prefab-less and the
		// adoption would report all rows unresolved.
		using var w = ItemSimWorld.Create();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		var control = (IEnemySyncControl)g1Enemies;
		var id = new NetworkEntityId(1, 8, 0);
		var key = new RuntimeEntityKey("cavetick", 1, 2, 2001, 3);
		var state = new EnemyEntity(id)
		{
			Position = new NetVector2(1f, 2f),
			SpawnPosition = new NetVector2(10f, 20f),
			PrefabId = "cavetick",
			RuntimeSpawned = true,
			CreationKey = key,
			Health = 100f,
		};
		var authority = w.G1.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.True(authority.TryUpsertEnemy(
			w.G1.SteamId,
			new EnemyState(new EntityId(1, 8, 0), "cavetick", 100f, true, false),
			out _,
			out var rejection), rejection?.Message);

		control.ApplyEnemySnapshot(0, new EnemySnapshotMsg
		{
			Enemies = [state.ToEnemyStateMsg()],
			RuntimeSpawns = [state.ToEnemySpawnEntryMsg()],
		});
		control.ApplyEnemyStream(new WireStateStream
		{
			EnemyStates = [Enemy(8, 30f, 40f).ToWireEnemyStreamState()],
		});

		var buffered = g1Enemies.GetEnemy(id);
		Assert.NotNull(buffered);
		Assert.Equal("cavetick", buffered!.PrefabId);
		Assert.True(buffered.RuntimeSpawned);
		Assert.Equal(10f, buffered.SpawnPosition.X);
		// The creation key is the runtime-spawn detail list's fact, not a row
		// field; the stream must not drop that list either.
		var spawn = Assert.Single(g1Enemies.RuntimeSpawns);
		Assert.Equal((RuntimeEntityKey?)key, spawn.CreationKey is null ? null : RuntimeEntityKey.FromKeyMsg(spawn.CreationKey));
	}

	[Fact]
	public void HeldSnapshot_IsFlushedWhenTheRosterBecomesReady()
	{
		// A member whose entry snapshot is held while the host generates must get it
		// at the ready transition: the completion marker may already have closed its
		// entry window, and the next repair wave is up to 60 s away.
		using var w = ItemSimWorld.Create();
		var hostEnemies = w.Host.Services.GetRequiredService<EnemySyncService>();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		var received = 0;
		g1Enemies.EnemySnapshotReceived += () => received++;
		hostEnemies.MarkRosterUnstable();
		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(50);
		Assert.Equal(0, received);

		hostEnemies.PublishEnemyStates([Enemy(9, 1f, 2f)]);
		hostEnemies.MarkRosterReady();
		w.Driver.Tick(50);

		Assert.True(received > 0, "the ready transition must flush the held snapshot");
		Assert.NotNull(g1Enemies.GetEnemy(new NetworkEntityId(1, 9, 0)));
	}
}
