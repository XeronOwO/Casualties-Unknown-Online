using System;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The cut's building-health rows against the live world at the cut instant. The
/// kernel keeps the health a blast reported; the game can fell that building
/// afterwards without reporting, and a cut that kept the old value named an entity
/// the continued world no longer had (2026-11-17: cell (5,492), health 1437.9, the
/// restore refused the row and called the world incomplete).
/// </summary>
public sealed class CutBuildingHealthReconcileTests
{
	[Fact]
	public void Apply_WhenTheBuildingStillStands_KeepsTheKernelsHealth()
	{
		var native = new FakeNativeWorldFacts();
		native.SeedBuilding(5.5f, 492.5f, 1437.9f);

		var reconciled = CutBuildingHealthReconcile.Apply(Checkpoint(1437.9f), native, WorldCutReason.Command, new RecordingLogger<CutBuildingHealthReconcileTests>());

		var row = Assert.Single(reconciled.WorldEntities!.BuildingHealth);
		Assert.Equal(1437.9f, row.Health);
	}

	[Fact]
	public void Apply_WhenTheBlastFelledTheBuilding_RecordsItDestroyed()
	{
		var native = new FakeNativeWorldFacts();
		var log = new RecordingLogger<CutBuildingHealthReconcileTests>();

		var reconciled = CutBuildingHealthReconcile.Apply(Checkpoint(1437.9f), native, WorldCutReason.Command, log);

		var row = Assert.Single(reconciled.WorldEntities!.BuildingHealth);
		Assert.Equal(0f, row.Health);
		Assert.Contains(log.Entries, entry => entry.Message.Contains("no longer stand there", StringComparison.Ordinal));
	}

	[Fact]
	public void Apply_WhenTheWorldMovedTheHealthOn_WritesTheLiveValue()
	{
		var native = new FakeNativeWorldFacts();
		native.SeedBuilding(5.5f, 492.5f, 600f);

		var reconciled = CutBuildingHealthReconcile.Apply(Checkpoint(1437.9f), native, WorldCutReason.Command, new RecordingLogger<CutBuildingHealthReconcileTests>());

		Assert.Equal(600f, Assert.Single(reconciled.WorldEntities!.BuildingHealth).Health);
	}

	[Fact]
	public void Apply_WithNoLiveWorld_KeepsTheKernelsFactsUntouched()
	{
		var native = new FakeNativeWorldFacts { LiveBuildingsPresent = false };
		var checkpoint = Checkpoint(1437.9f);

		var reconciled = CutBuildingHealthReconcile.Apply(checkpoint, native, WorldCutReason.Command, new RecordingLogger<CutBuildingHealthReconcileTests>());

		Assert.Same(checkpoint, reconciled);
	}

	[Fact]
	public void Apply_WithNoBuildingRows_DoesNotAskTheWorld()
	{
		var native = new FakeNativeWorldFacts();

		var reconciled = CutBuildingHealthReconcile.Apply(
			new GameCheckpoint(default, 1, [], WorldEntities: WorldEntityState.Empty),
			native,
			WorldCutReason.Command,
			new RecordingLogger<CutBuildingHealthReconcileTests>());

		Assert.Empty(reconciled.WorldEntities!.BuildingHealth);
		Assert.DoesNotContain("live-building-health", native.Calls);
	}

	private static GameCheckpoint Checkpoint(float health) =>
		new(
			default,
			1,
			[],
			WorldEntities: new WorldEntityState(
				[],
				[new BuildingEntityHealthFact(new EntityPosition(5, 492), health)],
				[],
				[]));
}
