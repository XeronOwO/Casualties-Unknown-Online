using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// An item left the world into a player's inventory: guest → host as a report
/// (the host arbitrates — first-writer-wins against the table — and broadcasts
/// the winner, source excluded; a refused pickup gets an ItemReject back) and
/// host → guest as the winner broadcast (the other guests remove the item from
/// their world).
/// </summary>
[PacketHandler(NetMsg.ItemPickup)]
public sealed class ItemPickupHandler(ILogger<ItemPickupHandler> log) : PacketHandlerBase<ItemPickupMsg>
{
	private readonly ILogger<ItemPickupHandler> _log = log;

	protected override void Handle(ulong sender, ItemPickupMsg msg, HandlerContext ctx)
	{
		ctx.Items.FireItemPickedUpReceived(sender, msg.ItemId);
		_log.LogInformation("Item pickup {ItemId} from {Sender}.", msg.ItemId, sender);
	}
}
