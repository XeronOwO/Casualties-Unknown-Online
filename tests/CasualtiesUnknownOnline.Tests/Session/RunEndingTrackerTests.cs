using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RunEndingTrackerCollection
{
	public const string Name = "RunEndingTracker";
}

/// <summary>
/// The ending patch records the branch before the kernel row for the same run carries it. A
/// projection that still says None must not erase that local fact; a run with a new id is the
/// boundary that clears it.
/// </summary>
[Collection(RunEndingTrackerCollection.Name)]
public class RunEndingTrackerTests
{
	[Fact]
	public void RunStart_BindsTheRunIdBeforeTheFirstBatch()
	{
		RunEndingTracker.Clear();
		RunEndingTracker.BeginRun(1UL);
		RunEndingTracker.Record(RunEnding.Merc);

		RunEndingTracker.Apply(1UL, RunEnding.None);

		Assert.Equal(RunEnding.Merc, RunEndingTracker.Current);
		Assert.False(RunEndingTracker.FromAuthority);
	}

	[Fact]
	public void NewRun_None_ClearsThePreviousEnding()
	{
		RunEndingTracker.Clear();
		RunEndingTracker.BeginRun(1UL);
		RunEndingTracker.Record(RunEnding.Merc);

		RunEndingTracker.BeginRun(2UL);
		RunEndingTracker.Apply(2UL, RunEnding.None);

		Assert.Equal(RunEnding.None, RunEndingTracker.Current);
		Assert.False(RunEndingTracker.FromAuthority);
	}

	[Fact]
	public void NewRun_Ending_AppliesTheAuthoritativeBranch()
	{
		RunEndingTracker.Clear();
		RunEndingTracker.BeginRun(1UL);

		RunEndingTracker.Apply(2UL, RunEnding.SurfaceBreach);

		Assert.Equal(RunEnding.SurfaceBreach, RunEndingTracker.Current);
		Assert.True(RunEndingTracker.FromAuthority);
	}

	[Fact]
	public void BeginRun_AfterAnEnding_ForgetsTheOldRun()
	{
		RunEndingTracker.Clear();
		RunEndingTracker.Apply(1UL, RunEnding.None);
		RunEndingTracker.Record(RunEnding.Merc);

		RunEndingTracker.BeginRun(2UL);

		Assert.Equal(RunEnding.None, RunEndingTracker.Current);
		Assert.Equal(2UL, RunEndingTracker.RunId);
	}
}
