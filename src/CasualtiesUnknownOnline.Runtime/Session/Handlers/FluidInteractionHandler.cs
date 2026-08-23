using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A fluid interaction (a consumed cell — drinking) arrived, star semantics:
/// the host executes it on its own grid (the cell non-empty → clear → relay;
/// already empty → ignore — every side is consistent) and relays to the other
/// members (the source excluded, it already applied locally). Guest: the
/// host's relay — clear the cell (idempotent).
/// </summary>
[PacketHandler(NetMsg.FluidInteraction, NetMessageDirection.Bidirectional)]
public sealed class FluidInteractionHandler(ILogger<FluidInteractionHandler> log) : PacketHandlerBase<FluidInteractionMsg, IWorldSessionHandlerContext>
{
	private readonly ILogger<FluidInteractionHandler> _log = log;

	protected override void Handle(ulong sender, FluidInteractionMsg msg, IWorldSessionHandlerContext ctx)
	{
		ctx.World.FireFluidInteractionReceived(sender, msg);
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.Session.BroadcastExcept(sender, NetMsg.FluidInteraction, msg);
		}

		_log.LogInformation("[Fluid] drink at=({X},{Y}) kind={Kind} from {Sender}.", msg.X, msg.Y, msg.Kind, sender);
	}
}
