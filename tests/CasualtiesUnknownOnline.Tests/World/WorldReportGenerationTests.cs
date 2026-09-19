using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The world/layer generation identity the direct world reports carry
/// (review/world-layer-generation-identity). Direct world reports are keyed by
/// block cell or world position, and those keys are LAYER-RELATIVE: after a
/// descent the same <c>(x, y)</c> addresses a freshly generated block, so a
/// receiver cannot tell a stale previous-layer report from a legitimate one
/// without an identity. The identity is the kernel run baseline —
/// <c>(RunEpoch, LayerIndex)</c> — and the comparison is one pure decision:
/// CURRENT means the report is about this world, STALE means it is about
/// another one and must be refused, UNKNOWN (no stamp, or no run on this side)
/// never counts as fresh.
/// </summary>
public class WorldReportGenerationTests
{
	private static readonly WorldReportGeneration Mine = new(RunEpoch: 7, LayerIndex: 3);

	[Fact]
	public void SameRunAndLayer_IsCurrent() =>
		Assert.Equal(
			WorldGenerationRelation.Current,
			WorldReportGeneration.Relate(Mine, new WorldGenerationMsg { RunEpoch = 7, LayerIndex = 3 }));

	[Fact]
	public void AnotherLayerOfTheSameRun_IsStale() =>
		Assert.Equal(
			WorldGenerationRelation.Stale,
			WorldReportGeneration.Relate(Mine, new WorldGenerationMsg { RunEpoch = 7, LayerIndex = 2 }));

	[Fact]
	public void AnotherRun_IsStale_EvenAtTheSameLayer()
	{
		// The layer index restarts with every run, so the epoch is what keeps a new
		// run's layer 3 from being taken for the previous run's layer 3 (matrix row
		// 5: the identity resets with the run baseline).
		Assert.Equal(
			WorldGenerationRelation.Stale,
			WorldReportGeneration.Relate(Mine, new WorldGenerationMsg { RunEpoch = 8, LayerIndex = 3 }));
	}

	[Fact]
	public void AReportWithoutAStamp_IsUnknown_NeverCurrent()
	{
		Assert.Equal(WorldGenerationRelation.Unknown, WorldReportGeneration.Relate(Mine, reported: null));
		Assert.Equal(WorldGenerationRelation.Unknown, WorldReportGeneration.Relate(current: null, reported: null));
	}

	[Fact]
	public void WithoutARunBaselineHere_AVerifiedLookingStamp_IsStillUnknown()
	{
		// This side cannot attribute anything before its own baseline exists, so a
		// stamp that would match later is not evidence now.
		Assert.Equal(
			WorldGenerationRelation.Unknown,
			WorldReportGeneration.Relate(current: null, new WorldGenerationMsg { RunEpoch = 7, LayerIndex = 3 }));
	}

	[Fact]
	public void Stamp_CarriesBothMembers_AndIsNullWithoutABaseline()
	{
		var stamp = WorldReportGeneration.Stamp(Mine);

		Assert.NotNull(stamp);
		Assert.Equal(7ul, stamp!.RunEpoch);
		Assert.Equal(3, stamp.LayerIndex);
		Assert.Null(WorldReportGeneration.Stamp(generation: null));
	}

	[Fact]
	public void Descriptions_NameBothSidesOfAComparison()
	{
		Assert.Equal("run 7 layer 3", WorldReportGeneration.Describe(Mine));
		Assert.Equal("run 7 layer 2", WorldReportGeneration.Describe(new WorldGenerationMsg { RunEpoch = 7, LayerIndex = 2 }));
		Assert.Equal("no generation", WorldReportGeneration.Describe((WorldReportGeneration?)null));
		Assert.Equal("no generation stamp", WorldReportGeneration.Describe((WorldGenerationMsg?)null));
	}

	[Fact]
	public void KernelSource_IsNullUntilARunIsCommitted_ThenReadsTheBaseline()
	{
		var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
		var source = new KernelWorldGenerationSource(authority);

		Assert.Null(source.Current);
		Assert.Null(source.Stamp());

		Assert.True(authority.TryStartRun(
			1UL,
			new RunState(1UL, [1, 2, 3, 4, 5, 6, 7, 8], 0, 0, 0, false, null, 4),
			out _,
			out _));

		var epoch = authority.CreateCheckpoint().RunEpoch.Value;
		Assert.Equal(new WorldReportGeneration(epoch, 4), source.Current);
		Assert.Equal(epoch, source.Stamp()!.RunEpoch);
		Assert.Equal(4, source.Stamp()!.LayerIndex);
	}
}
