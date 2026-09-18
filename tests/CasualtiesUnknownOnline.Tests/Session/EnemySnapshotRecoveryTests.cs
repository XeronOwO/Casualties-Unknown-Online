using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The enemy binding recovery (sync-coverage audit row N1): the world-entry /
/// reconnect <c>EnemySnapshot</c> is a one-shot, and a member that STAYS in the
/// world has no second chance — unless the in-session repair group carries the
/// enemy table like it carries every other absolute in-world table. The pairing
/// key is the enemy's SPAWN position (the host's bind-time anchor), never its
/// live position: the guest's copies are frozen at their spawn spots, so a
/// repair snapshot published later can only pair against the anchor.
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

	/// <summary>An enemy whose live position has moved away from the anchor the host recorded when it bound it.</summary>
	private static EnemyEntity Anchored(NetworkEntityId id, NetVector2 live, NetVector2 spawn) =>
		new(id)
		{
			Position = live,
			SpawnPosition = spawn,
			Velocity = new NetVector2(0f, 0f),
			Health = 100f,
			PrefabId = "cavetick",
		};

	[Fact]
	public void EnemiesPublishedAfterTheEntryEdge_StillReachAStayingMember()
	{
		// g1 enters while the host's enemy table is still empty: the entry
		// fan-out's SendEnemySnapshot no-ops ("an empty table is a no-op — the
		// member's own generated enemies stay"), and no further InWorld edge is
		// coming while it stays in the world. The in-session repair group is the
		// only path left that can still bind it (N1).
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

		Assert.True(snapshots == 1, $"the in-session repair must deliver the enemy snapshot, got {snapshots}");
		var bound = g1Enemies.GetEnemy(new NetworkEntityId(1, 3, 0));
		Assert.True(bound is not null, "the staying member must learn the host's enemy id and its binding facts");
		Assert.Equal(-13f, bound!.Position.X);
		Assert.Equal(466.8f, bound.Position.Y);
	}

	[Fact]
	public void EmptyEnemyTable_TheRepairSendsNothing()
	{
		// The documented no-op stays: with no host enemies there is nothing to
		// bind, and a spurious empty snapshot would only overwrite the guest's
		// own generated set with nothing.
		using var w = ItemSimWorld.Create();
		var frames = new List<NetMsg>();
		w.G1.Transport.MessageReceived += (_, frame) => frames.Add((NetMsg)frame[0]);

		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(50);

		Assert.DoesNotContain(NetMsg.EnemySnapshot, frames);
	}

	[Fact]
	public void SnapshotCarriesTheSpawnAnchor_BesideTheLivePosition()
	{
		// The guest must receive BOTH: the live position (presentation) and the
		// host's bind-time anchor (the pairing key). Collapsing them into one
		// field is exactly the defect — the frozen copy can only pair on the
		// anchor once the host's enemy has walked away.
		using var w = ItemSimWorld.Create();
		var hostEnemies = w.Host.Services.GetRequiredService<EnemySyncService>();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		var id = new NetworkEntityId(1, 4, 0);

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		hostEnemies.PublishEnemyStates([Anchored(id, live: new NetVector2(40f, 55f), spawn: new NetVector2(10f, 20f))]);
		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(50);

		var bound = g1Enemies.GetEnemy(id);
		Assert.True(bound is not null, "the repair snapshot must bind the id on the guest");
		Assert.Equal(40f, bound!.Position.X);
		Assert.Equal(55f, bound.Position.Y);
		Assert.Equal(10f, bound.SpawnPosition.X);
		Assert.Equal(20f, bound.SpawnPosition.Y);
	}

	[Fact]
	public void RepairSnapshot_RepeatsTheSameBinding_AndRefreshesTheLivePosition()
	{
		// The 60 s repair keeps arriving while the member stays in the world: each
		// pass must be idempotent (one entry per id) and must move only the live
		// position — the anchor is what a re-pairing compares, so a repair that
		// rewrote it would break the next one.
		using var w = ItemSimWorld.Create();
		var hostEnemies = w.Host.Services.GetRequiredService<EnemySyncService>();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		var repair = w.Host.Services.GetRequiredService<WorldEntryFanout>();
		var id = new NetworkEntityId(1, 4, 0);

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		hostEnemies.PublishEnemyStates([Anchored(id, live: new NetVector2(11f, 21f), spawn: new NetVector2(10f, 20f))]);
		repair.SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(50);

		hostEnemies.PublishEnemyStates([Anchored(id, live: new NetVector2(40f, 55f), spawn: new NetVector2(10f, 20f))]);
		repair.SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(50);

		var bound = g1Enemies.GetEnemy(id);
		Assert.True(bound is not null, "the id stays bound across repairs");
		Assert.Equal(40f, bound!.Position.X);
		Assert.Equal(10f, bound.SpawnPosition.X);
		Assert.Equal(20f, bound.SpawnPosition.Y);
		Assert.True(g1Enemies.Enemies.Count() == 1, $"a repeat snapshot never duplicates the id, got {g1Enemies.Enemies.Count()}");
	}

	[Fact]
	public void StreamFrames_DoNotForgetTheBindingAnchor()
	{
		// The 20 Hz stream is update-only and its payload has no anchor, so a
		// stream frame that rebuilt the buffered entry from scratch would silently
		// zero the pairing key — the next pairing would then compare (0,0) against
		// the guest's frozen copy, fail the tolerance and clear the mapping.
		using var w = ItemSimWorld.Create();
		var g1Enemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		var control = (IEnemySyncControl)g1Enemies;
		var id = new NetworkEntityId(1, 5, 0);

		control.ApplyEnemySnapshot(new EnemySnapshotMsg
		{
			Enemies = [Anchored(id, live: new NetVector2(1f, 2f), spawn: new NetVector2(10f, 20f)).ToEnemyStateMsg()],
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
	public void PairingPremise_TheLivePositionStopsMatching_WhileTheSpawnAnchorStillPairs()
	{
		// The mechanism this ticket fixes, pinned without the adapter: the guest's
		// copies are frozen at their spawn positions, so pairing against the
		// host's CURRENT position works only in the instant after generation. A
		// repair snapshot therefore needs the spawn anchor as its key — otherwise
		// it fails the tolerance for the whole set (the arbitration is
		// all-or-nothing) and, worse, clears the guest's mapping-established flag.
		var spawn = new NetVector2(10f, 20f);
		var moved = new NetVector2(40f, 55f);

		Assert.False(
			EnemySpawnArbitration.TryPair([moved], [spawn], out _),
			"a repair snapshot carrying only the live position cannot pair after the enemy walked away");
		Assert.True(
			EnemySpawnArbitration.TryPair([spawn], [spawn], out var pairs),
			"the spawn anchor pairs at any later time");
		Assert.True(pairs.Count == 1, "the anchor pairs index-by-index");
	}
}
