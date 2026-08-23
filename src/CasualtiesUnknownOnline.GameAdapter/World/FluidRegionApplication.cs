using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The guest side of the fluid domain: the host's streamed regions are applied
/// onto the local grid. Every region is an ABSOLUTE RLE snapshot of its
/// rectangle — each cell in the rectangle is covered (trailing zero runs are
/// omitted, the decoder clears the rest), so an apply is idempotent and a lost
/// message is healed by the next one. The DECODE is the pure FluidRleCodec
/// (tested); this class only binds it to the game grid. The game's own
/// renderer (RenderFluids) draws the applied grid unchanged.
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

		FluidRleCodec.Decode(
			msg.Cells, msg.Width, msg.Height, msg.OriginX, msg.OriginY,
			(int)world.width, (int)world.height,
			(x, y, value) => fluid.fluid[x, y] = value);

		_log.LogDebug("[Fluid] applied=(x={X},y={Y},w={W},h={H}) seq={Seq}.", msg.OriginX, msg.OriginY, msg.Width, msg.Height, msg.Seq);
	}
}
