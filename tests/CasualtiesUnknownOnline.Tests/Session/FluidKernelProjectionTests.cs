using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class FluidKernelProjectionTests
{
	[Fact]
	public void Sync_UpsertsAndClearsStalePositiveChunks()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var projection = host.Services.GetRequiredService<FluidKernelProjection>();

		projection.Sync(
		[
			new FluidRegionSummary(0, 0, 5, 1),
			new FluidRegionSummary(1, 2, 3, 2),
		]);

		var table = authority.QueryFluids()!;
		Assert.Contains(table.Regions, r => r.ChunkX == 0 && r.ChunkY == 0 && r.TotalAmount == 5 && r.MainType == 1);
		Assert.Contains(table.Regions, r => r.ChunkX == 1 && r.ChunkY == 2 && r.TotalAmount == 3 && r.MainType == 2);

		projection.Sync([new FluidRegionSummary(0, 0, 5, 1)]);

		var cleared = authority.QueryFluids()!.Regions.Single(r => r.ChunkX == 1 && r.ChunkY == 2);
		Assert.Equal(0, cleared.TotalAmount);
		Assert.Equal(0, cleared.MainType);
	}

	[Fact]
	public void WorldControlReportFluidRegions_CommitsThroughProjection()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var world = host.Services.GetRequiredService<IWorldControl>();

		world.ReportFluidRegions([new FluidRegionSummary(9, 8, 4, 2)]);

		var region = Assert.Single(authority.QueryFluids()!.Regions);
		Assert.Equal(9, region.ChunkX);
		Assert.Equal(8, region.ChunkY);
		Assert.Equal(4, region.TotalAmount);
		Assert.Equal(2, region.MainType);
	}

	[Fact]
	public void Sync_DoesNotCommitUnchangedRegionFacts()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var projection = host.Services.GetRequiredService<FluidKernelProjection>();

		projection.Sync([new FluidRegionSummary(4, 5, 7, 3)]);
		var before = authority.CurrentGlobalRevision;

		projection.Sync([new FluidRegionSummary(4, 5, 7, 3)]);

		Assert.Equal(before, authority.CurrentGlobalRevision);
	}
}
