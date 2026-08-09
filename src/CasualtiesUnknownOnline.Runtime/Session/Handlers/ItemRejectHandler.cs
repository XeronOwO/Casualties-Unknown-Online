using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host refused our item arbitration (pickup of an item that is not in
/// the authoritative table — spawn report in flight or already taken). The
/// guest rolls its optimistic pickup back: the item leaves the inventory and
/// goes back into the world.
/// </summary>
[PacketHandler(NetMsg.ItemReject)]
public sealed class ItemRejectHandler(ILogger<ItemRejectHandler> log) : PacketHandlerBase<ItemRejectMsg>
{
	private readonly ILogger<ItemRejectHandler> _log = log;

	protected override void Handle(ulong sender, ItemRejectMsg msg, HandlerContext ctx)
	{
		ctx.Items.FireItemRejectReceived(sender, msg.ItemId, msg.Rejection);
		_log.LogWarning("Item {ItemId} rejected ({Reason}) by the host {Sender}.", msg.ItemId, msg.Rejection, sender);
	}
}
