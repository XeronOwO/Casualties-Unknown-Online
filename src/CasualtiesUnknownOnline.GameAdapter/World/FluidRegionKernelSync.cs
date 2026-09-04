using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;
using System;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Host side of the fluid region kernel checkpoint: at a low cadence it
/// aggregates the authoritative Unity fluid grid into coarse world-chunk
/// totals/dominant types and reports them through <see cref="IWorldControl"/>.
/// The high-frequency RLE viewport stream remains unchanged; this projection
/// only feeds the persistent kernel/checkpoint domain.
/// </summary>
internal sealed class FluidRegionKernelSync(
	IWorldControl world,
	ISessionControl session,
	ILogger<FluidRegionKernelSync> log)
{
	private const float SummaryInterval = 5f;

	private readonly IWorldControl _world = world;
	private readonly ISessionControl _session = session;
	private readonly ILogger<FluidRegionKernelSync> _log = log;
	private float _nextSummary;

	internal void Update()
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		var now = Time.time;
		if (now < _nextSummary)
		{
			return;
		}

		_nextSummary = now + SummaryInterval;

		var fluid = FluidManager.main;
		var worldGen = WorldGeneration.world;
		if (fluid == null || worldGen == null) // Unity objects — ==
		{
			return;
		}

		var width = (int)worldGen.width;
		var height = (int)worldGen.height;
		var chunkSize = WorldGeneration.CHUNKSIZE;
		var summaries = new List<FluidRegionSummary>();
		var counts = new int[256];

		for (var chunkY = 0; chunkY < height; chunkY += chunkSize)
		{
			var y1 = Mathf.Min(chunkY + chunkSize, height);
			for (var chunkX = 0; chunkX < width; chunkX += chunkSize)
			{
				var x1 = Mathf.Min(chunkX + chunkSize, width);
				Array.Clear(counts, 0, counts.Length);
				var total = 0;
				for (var y = chunkY; y < y1; y++)
				{
					for (var x = chunkX; x < x1; x++)
					{
						var type = fluid.fluid[x, y];
						if (type == 0)
						{
							continue;
						}

						total++;
						counts[type]++;
					}
				}

				if (total == 0)
				{
					continue;
				}

				var mainType = (byte)0;
				var maxCount = 0;
				for (var type = 1; type < counts.Length; type++)
				{
					if (counts[type] <= maxCount)
					{
						continue;
					}

					maxCount = counts[type];
					mainType = (byte)type;
				}

				summaries.Add(new FluidRegionSummary(
					chunkX / chunkSize,
					chunkY / chunkSize,
					total,
					mainType));
			}
		}

		_world.ReportFluidRegions(summaries);
		_log.LogDebug("[FluidKernel] reported {Count} non-empty region chunk(s).", summaries.Count);
	}
}
