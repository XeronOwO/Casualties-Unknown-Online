using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The guest side of the fluid domain: the host's streamed regions are applied
/// onto the local grid. Every region is an ABSOLUTE RLE snapshot of its
/// rectangle — each cell in the rectangle is covered (trailing zero runs are
/// omitted, the decoder clears the rest), so an apply is idempotent and a lost
/// message is healed by the next one. The game's own renderer (RenderFluids)
/// draws the applied grid unchanged.
/// </summary>
internal sealed class FluidRegionApplication(ILogger<FluidRegionApplication> log)
{
	private readonly ILogger<FluidRegionApplication> _log = log;

	internal void Apply(FluidRegionMsg msg)
	{
		var fluid = FluidManager.main;
		var world = WorldGeneration.world;
		if (fluid == null || world == null) // Unity objects — == (no world yet — the region is dropped, the next one covers it)
		{
			return;
		}

		var width = msg.Width;
		var height = msg.Height;
		var cells = msg.Cells;
		var total = width * height;
		var pos = 0;
		for (var i = 0; i + 1 < cells.Length && pos < total; i += 2)
		{
			var value = cells[i];
			var count = cells[i + 1];
			for (var c = 0; c < count && pos < total; c++, pos++)
			{
				if (value != 0)
				{
					var x = msg.OriginX + pos % width;
					var y = msg.OriginY + pos / width;
					fluid.fluid[Mathf.Clamp(x, 0, (int)world.width - 1), Mathf.Clamp(y, 0, (int)world.height - 1)] = value;
				}
			}
		}

		// The uncovered tail (the omitted trailing zero runs) = cleared cells.
		for (; pos < total; pos++)
		{
			var x = msg.OriginX + pos % width;
			var y = msg.OriginY + pos / width;
			fluid.fluid[Mathf.Clamp(x, 0, (int)world.width - 1), Mathf.Clamp(y, 0, (int)world.height - 1)] = 0;
		}

		_log.LogInformation("[Fluid] applied=(x={X},y={Y},w={W},h={H}) seq={Seq}.", msg.OriginX, msg.OriginY, width, height, msg.Seq);
	}
}
