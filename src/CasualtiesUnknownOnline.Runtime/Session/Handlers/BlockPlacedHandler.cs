using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A block was placed / air-written: guest → host as a report (the host
/// arbitrates — a placement must land on air, an air write on something — then
/// applies and relays to every member, the reporter included; a refused report
/// gets the host's current cell back) and host → guest as that answer. The
/// answer happens after arbitration, so this handler only surfaces the event;
/// the adapter validates and answers via BroadcastBlockPlaced /
/// SendBlockPlacedCorrection.
/// </summary>
[PacketHandler(NetMsg.BlockPlaced, NetMessageDirection.Bidirectional)]
public sealed class BlockPlacedHandler(ILogger<BlockPlacedHandler> log) : PacketHandlerBase<BlockPlacedMsg, IWorldHandlerContext>
{
	private readonly ILogger<BlockPlacedHandler> _log = log;

	protected override void Handle(ulong sender, BlockPlacedMsg msg, IWorldHandlerContext ctx)
	{
		ctx.World.FireBlockPlacedReceived(sender, msg.X, msg.Y, msg.Block);
		_log.LogDebug("Block placed at ({X},{Y}) type {Block} from {Sender}.", msg.X, msg.Y, msg.Block, sender);
	}
}
