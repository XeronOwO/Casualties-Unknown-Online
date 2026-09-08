using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// Phase-2 entity-event simulations: the transient one-way edges (mine press, unstable-crystal tick, repeatable teleport) that must never enter the late-joiner checkpoint nor clobber the durable snapshot fact.
/// Split from EntityEventSimulationTests so xUnit v2 (serial inside a class) does
/// not serialize every hand-written simulation in one collection. The world and
/// the host-executor shell stay shared in EntityEventSimWorld.
/// </summary>
[Trait("Category", "Integration")]
public class EntityEventSimulationTransientTests
{
	[Fact]
	public void Snapshot_ElapsedZero_ForLiveEvents()
	{
		// A live event's ElapsedSeconds is 0 (the transition just happened) —
		// the replay runs the full transition, the original behaviour.
		var w = EntityEventSimWorld.Create();
		w.Trigger(w.G1, EntityEventKind.SpikeStabbed, 10f, 20f);

		Assert.True(w.G2Events.Count == 1, "the relay must arrive");
		Assert.True(w.G2Events[0].ElapsedSeconds == 0f, $"a live event carries no elapsed, got {w.G2Events[0].ElapsedSeconds}");
	}

	[Fact]
	public void MinePressed_TransientEdge_NotInLateJoinerCheckpoint()
	{
		var w = EntityEventSimWorld.Create();
		var consumed = new List<IReadOnlyList<EntityEventMsg>>();
		w.G1.Services.GetRequiredService<WorldEntityKernelProjection>().TrapSnapshotProjected += list => consumed.Add(list);

		w.Trigger(w.G1, EntityEventKind.MinePressed, 10f, 20f);
		w.SendCheckpoint(w.G1);

		Assert.True(consumed.Count == 0,
			$"the transient press edge must not occupy a snapshot slot, got {consumed.Count} snapshot(s)");
	}

	[Fact]
	public void MinePressed_DoesNotClobberMineExplodedSnapshotFact()
	{
		var w = EntityEventSimWorld.Create();
		var consumed = new List<IReadOnlyList<EntityEventMsg>>();
		w.G1.Services.GetRequiredService<WorldEntityKernelProjection>().TrapSnapshotProjected += list => consumed.Add(list);

		w.Trigger(w.G1, EntityEventKind.MinePressed, 10f, 20f);
		w.Trigger(w.G1, EntityEventKind.MineExploded, 10f, 20f);
		w.SendCheckpoint(w.G1);

		Assert.True(consumed.Count == 1 && consumed[0].Count == 1,
			$"only the durable MineExploded consumption is snapshotted, got {consumed.Count} snapshot(s) with {(consumed.Count > 0 ? consumed[0].Count : 0)} entry/entries");
		Assert.True(consumed[0][0].Kind == EntityEventKind.MineExploded,
			$"the snapshot carries MineExploded, got {consumed[0][0].Kind}");
	}

	[Fact]
	public void CrystalUnstableTicked_TransientEdge_NotInLateJoinerCheckpoint()
	{
		var w = EntityEventSimWorld.Create();
		var consumed = new List<IReadOnlyList<EntityEventMsg>>();
		w.G1.Services.GetRequiredService<WorldEntityKernelProjection>().TrapSnapshotProjected += list => consumed.Add(list);

		w.Trigger(w.G1, EntityEventKind.CrystalUnstableTicked, 10f, 20f);
		w.SendCheckpoint(w.G1);

		Assert.True(consumed.Count == 0,
			$"the transient ticking edge must not occupy a snapshot slot, got {consumed.Count} snapshot(s)");
	}

	[Fact]
	public void CrystalUnstableTicked_DoesNotClobberCrystalUnstableExplodedSnapshotFact()
	{
		var w = EntityEventSimWorld.Create();
		var consumed = new List<IReadOnlyList<EntityEventMsg>>();
		w.G1.Services.GetRequiredService<WorldEntityKernelProjection>().TrapSnapshotProjected += list => consumed.Add(list);

		w.Trigger(w.G1, EntityEventKind.CrystalUnstableTicked, 10f, 20f);
		w.Trigger(w.G1, EntityEventKind.CrystalUnstableExploded, 10f, 20f);
		w.SendCheckpoint(w.G1);

		Assert.True(consumed.Count == 1 && consumed[0].Count == 1,
			$"only the durable CrystalUnstableExploded consumption is snapshotted, got {consumed.Count} snapshot(s) with {(consumed.Count > 0 ? consumed[0].Count : 0)} entry/entries");
		Assert.True(consumed[0][0].Kind == EntityEventKind.CrystalUnstableExploded,
			$"the snapshot carries CrystalUnstableExploded, got {consumed[0][0].Kind}");
	}

	[Fact]
	public void CrystalTeleportTriggered_RepeatableEvent_NotInLateJoinerCheckpoint()
	{
		var w = EntityEventSimWorld.Create();
		var consumed = new List<IReadOnlyList<EntityEventMsg>>();
		w.G1.Services.GetRequiredService<WorldEntityKernelProjection>().TrapSnapshotProjected += list => consumed.Add(list);

		w.Trigger(w.G1, EntityEventKind.CrystalTeleportTriggered, 10f, 20f);
		w.SendCheckpoint(w.G1);

		Assert.True(consumed.Count == 0,
			$"the repeatable teleport laugh/flash must not occupy a snapshot slot, got {consumed.Count} snapshot(s)");
	}
}
