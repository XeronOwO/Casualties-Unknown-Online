using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Runtime.Time;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// Phase-2 entity-event simulations: the checkpoint/snapshot lifecycle (one-shot consumption snapshots, opened-entity positions, reset clears, trigger age).
/// Split from EntityEventSimulationTests so xUnit v2 (serial inside a class) does
/// not serialize every hand-written simulation in one collection. The world and
/// the host-executor shell stay shared in EntityEventSimWorld.
/// </summary>
public class EntityEventSimulationCheckpointTests
{
	[Fact]
	public void OneShotConsumption_CheckpointCarriesTheLatest()
	{
		var w = EntityEventSimWorld.Create();
		var g1Consumed = new List<IReadOnlyList<EntityEventMsg>>();
		w.G1.Services.GetRequiredService<WorldEntityKernelProjection>().TrapSnapshotProjected += list => g1Consumed.Add(list);

		// The same one-shot entity progresses (ScrapEaterProgress carries the %).
		w.HostChannel.ReportTrapConsumed(EntityEventKind.ScrapEaterProgress, 30f, 40f, extra: 25);
		w.HostChannel.ReportTrapConsumed(EntityEventKind.ScrapEaterProgress, 30f, 40f, extra: 50); // overwrites
		w.SendCheckpoint(w.G1);

		Assert.True(g1Consumed.Count == 1, "the snapshot must arrive");
		Assert.True(g1Consumed[0].Count == 1, $"one consumed entity, got {g1Consumed[0].Count}");
		Assert.True(g1Consumed[0][0].Kind == EntityEventKind.ScrapEaterProgress && g1Consumed[0][0].Extra == 50,
			"the latest consumption (progress 50) is what the late joiner replays");
	}

	[Fact]
	public void OneShotConsumption_ResetClears_NewWorldStartsEmpty()
	{
		var w = EntityEventSimWorld.Create();
		w.Trigger(w.G1, EntityEventKind.MineExploded, 10f, 20f);
		w.HostChannel.ResetConsumptions(); // a new layer is generating

		var g2Consumed = new List<IReadOnlyList<EntityEventMsg>>();
		w.G2.Services.GetRequiredService<WorldEntityKernelProjection>().TrapSnapshotProjected += list => g2Consumed.Add(list);
		w.SendCheckpoint(w.G2);

		Assert.True(g2Consumed.Count == 0, "an empty consumption table sends nothing");
	}

	[Fact]
	public void CheckpointProjection_LateJoinerConsumesEveryEntry()
	{
		var w = EntityEventSimWorld.Create();
		w.HostChannel.ReportTrapConsumed(EntityEventKind.MineExploded, 10f, 20f, extra: 0);
		w.HostChannel.ReportTrapConsumed(EntityEventKind.BioTerminalUnlocked, 30f, 40f, extra: 0);

		w.SendCheckpoint(w.G1);

		// The late joiner replays every entry against its regenerated world
		// (the snapshot-consumption step) — two consumed entities, two replays.
		Assert.True(w.G1Replays.Value == 2, $"the late joiner must consume every entry, got {w.G1Replays.Value}");
	}

	[Fact]
	public void CheckpointProjection_DuplicateCheckpoint_ConsumesOnce()
	{
		var w = EntityEventSimWorld.Create();
		w.HostChannel.ReportTrapConsumed(EntityEventKind.MineExploded, 10f, 20f, extra: 0);

		w.SendCheckpoint(w.G1);
		w.SendCheckpoint(w.G1); // a periodic checkpoint resend

		Assert.True(w.G1Replays.Value == 1, $"a duplicate snapshot must not re-consume, got {w.G1Replays.Value}");
	}

	[Fact]
	public void OpenedEntity_CheckpointCarriesEveryDistinctPosition()
	{
		var w = EntityEventSimWorld.Create();
		var g1Opened = new List<IReadOnlyList<NetVector2Msg>>();
		w.G1.Services.GetRequiredService<WorldEntityKernelProjection>().OpenedEntitiesProjected += list => g1Opened.Add(list);

		w.HostChannel.ReportOpenedEntity(10.2f, 20.8f);
		w.HostChannel.ReportOpenedEntity(30f, 40f);
		w.HostChannel.ReportOpenedEntity(10.7f, 20.1f); // the same cell — idempotent
		w.SendCheckpoint(w.G1);

		Assert.True(g1Opened.Count == 1, "the snapshot must arrive");
		Assert.True(g1Opened[0].Count == 2, $"two distinct cells, got {g1Opened[0].Count}");
	}

	[Fact]
	public void OpenedEntity_ResetClears_NewWorldStartsEmpty()
	{
		var w = EntityEventSimWorld.Create();
		w.HostChannel.ReportOpenedEntity(10f, 20f);
		w.HostChannel.ResetOpenedEntities(); // a new layer is generating

		var g2Opened = new List<IReadOnlyList<NetVector2Msg>>();
		w.G2.Services.GetRequiredService<WorldEntityKernelProjection>().OpenedEntitiesProjected += list => g2Opened.Add(list);
		w.SendCheckpoint(w.G2);

		Assert.True(g2Opened.Count == 0, "an empty opened table sends nothing");
	}

	[Fact]
	public void Checkpoint_ElapsedCarriesTheTriggerAge()
	{
		// The rejoin scenario (user-verified): the host's shuttle door opened,
		// MINUTES pass, the guest rejoins — the snapshot must carry how long
		// ago, so the door's replay lands at the CURRENT state (already open /
		// gone) instead of re-running the 10 s opening animation from zero.
		var w = EntityEventSimWorld.Create();
		var clock = (FakeClock)w.Host.Services.GetRequiredService<ITimeSource>();
		w.HostChannel.ReportTrapConsumed(EntityEventKind.ShuttleDoorOpened, 0f, 496f, extra: 0);
		clock.Advance(7_500); // 7.5 s later — the door's animation is mid-flight

		var g1Consumed = new List<IReadOnlyList<EntityEventMsg>>();
		w.G1.Services.GetRequiredService<WorldEntityKernelProjection>().TrapSnapshotProjected += list => g1Consumed.Add(list);
		w.SendCheckpoint(w.G1);

		Assert.True(g1Consumed.Count == 1, "the snapshot must arrive");
		Assert.True(g1Consumed[0][0].ElapsedSeconds > 7.4f && g1Consumed[0][0].ElapsedSeconds < 7.6f,
			$"the elapsed must ride the snapshot (7.5 s), got {g1Consumed[0][0].ElapsedSeconds}");
	}
}
