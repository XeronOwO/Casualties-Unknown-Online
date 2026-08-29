using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Projects host-derived coarse fluid-region summaries into the kernel fluid
/// table. The projection is change-gated so the low-cadence aggregation does
/// not spam kernel commits; chunks that were non-empty and then emptied are
/// written back as zero totals so the authoritative checkpoint does not retain
/// stale positive volume.
/// </summary>
public sealed class FluidKernelProjection(
	ItemKernelAuthority kernelAuthority,
	ISessionControl session,
	ITimeSource time,
	ILogger<FluidKernelProjection> log)
{
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;
	private readonly ISessionControl _session = session;
	private readonly ITimeSource _time = time;
	private readonly ILogger<FluidKernelProjection> _log = log;

	public void Sync(IReadOnlyList<FluidRegionSummary> regions)
	{
		var table = _kernelAuthority.QueryFluids();
		var desired = new HashSet<(int ChunkX, int ChunkY)>();

		foreach (var region in regions)
		{
			desired.Add((region.ChunkX, region.ChunkY));
			var current = table?.Regions.FirstOrDefault(r =>
				r.ChunkX == region.ChunkX && r.ChunkY == region.ChunkY);
			if (current is not null
				&& current.TotalAmount == region.TotalAmount
				&& current.MainType == region.MainType)
			{
				continue;
			}

			Commit(region);
		}

		if (table is null)
		{
			return;
		}

		// A region that left the host's non-empty set must not keep its old
		// positive total in the checkpoint. Write a zero fact so the table has
		// the full truth for every chunk it has ever seen.
		foreach (var stale in table.Regions.Where(r =>
			r.TotalAmount != 0 && !desired.Contains((r.ChunkX, r.ChunkY))))
		{
			Commit(new FluidRegionSummary(stale.ChunkX, stale.ChunkY, 0, 0));
		}
	}

	private void Commit(FluidRegionSummary summary)
	{
		var state = new FluidRegionState(
			summary.ChunkX,
			summary.ChunkY,
			summary.TotalAmount,
			summary.MainType,
			_time.NowMs);
		if (_kernelAuthority.TryUpdateFluidRegion(
			_session.LocalSteamId,
			state,
			out _,
			out var rejection))
		{
			_log.LogDebug(
				"[FluidKernel] committed region chunk=({X},{Y}) total={Total} main={Main}.",
				summary.ChunkX, summary.ChunkY, summary.TotalAmount, summary.MainType);
		}
		else
		{
			_log.LogWarning(
				"[FluidKernel] rejected region chunk=({X},{Y}) total={Total} main={Main}: {Reason} ({Message}).",
				summary.ChunkX, summary.ChunkY, summary.TotalAmount, summary.MainType,
				rejection!.Reason, rejection.Message);
		}
	}
}
