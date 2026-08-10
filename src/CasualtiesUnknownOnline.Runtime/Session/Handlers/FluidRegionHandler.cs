using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host's fluid-grid region arrived (unreliable state stream): apply it
/// onto the local grid (the game's own renderer then draws it). The host is
/// the fluid authority — the guest never simulates, it renders the streamed
/// regions; every region is an absolute RLE snapshot of its rectangle, so an
/// apply is idempotent and a lost message is healed by the next one.
/// </summary>
[PacketHandler(NetMsg.FluidRegion)]
public sealed class FluidRegionHandler(ILogger<FluidRegionHandler> log) : PacketHandlerBase<FluidRegionMsg>
{
	private readonly ILogger<FluidRegionHandler> _log = log;

	protected override void Handle(ulong sender, FluidRegionMsg msg, HandlerContext ctx)
	{
		ctx.World.FireFluidRegionReceived(msg);
		_log.LogInformation("[Fluid] region=(x={X},y={Y},w={W},h={H}) cells={Cells} seq={Seq} from {Sender}.",
			msg.OriginX, msg.OriginY, msg.Width, msg.Height, msg.Cells.Length, msg.Seq, sender);
	}
}
