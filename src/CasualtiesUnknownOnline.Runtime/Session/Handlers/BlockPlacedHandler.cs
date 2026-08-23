using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A block was placed: guest → host as a report (the host arbitrates — the
/// target must be air — then applies and relays to the other guests, source
/// excluded) and host → guest as a broadcast (the host's own placement). The
/// broadcast happens after arbitration, so this handler only surfaces the
/// event; the adapter validates and answers via BroadcastBlockPlaced.
/// </summary>
[PacketHandler(NetMsg.BlockPlaced, NetMessageDirection.Bidirectional)]
public sealed class BlockPlacedHandler(ILogger<BlockPlacedHandler> log) : PacketHandlerBase<BlockPlacedMsg, IWorldHandlerContext>
{
	private readonly ILogger<BlockPlacedHandler> _log = log;

	protected override void Handle(ulong sender, BlockPlacedMsg msg, IWorldHandlerContext ctx)
	{
		ctx.World.FireBlockPlacedReceived(sender, msg.X, msg.Y, msg.Block);
		_log.LogInformation("Block placed at ({X},{Y}) type {Block} from {Sender}.", msg.X, msg.Y, msg.Block, sender);
	}
}
