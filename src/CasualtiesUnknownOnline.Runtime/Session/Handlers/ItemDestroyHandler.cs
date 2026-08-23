using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A world item was destroyed (decay to zero, consumed): guest → host as a
/// report (the host drops it from the authoritative table and relays, source
/// excluded), host → guest as a broadcast relay.
/// </summary>
[PacketHandler(NetMsg.ItemDestroy, NetMessageDirection.Bidirectional)]
public sealed class ItemDestroyHandler(ILogger<ItemDestroyHandler> log) : PacketHandlerBase<ItemDestroyMsg, IItemHandlerContext>
{
	private readonly ILogger<ItemDestroyHandler> _log = log;

	protected override void Handle(ulong sender, ItemDestroyMsg msg, IItemHandlerContext ctx)
	{
		ctx.Items.FireItemDestroyedReceived(sender, msg.ItemId);
		_log.LogInformation("Item destroy {ItemId} from {Sender}.", msg.ItemId, sender);
	}
}
