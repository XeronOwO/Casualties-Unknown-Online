using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// An item left the world into a player's inventory: guest → host as a report
/// (the host arbitrates — first-writer-wins against the table, the transfer
/// happens from the host's OWN entry, and the picker's digest evidence is
/// checked afterwards — accept-with-correction; a refused pickup gets an
/// ItemReject back) and host → guest as the winner broadcast (the other guests
/// remove the item from their world).
/// </summary>
[PacketHandler(NetMsg.ItemPickup, NetMessageDirection.Bidirectional)]
public sealed class ItemPickupHandler(ILogger<ItemPickupHandler> log) : PacketHandlerBase<ItemPickupMsg, IItemHandlerContext>
{
	private readonly ILogger<ItemPickupHandler> _log = log;

	protected override void Handle(ulong sender, ItemPickupMsg msg, IItemHandlerContext ctx)
	{
		ctx.Items.FireItemPickedUpReceived(sender, msg.ItemId, msg.Item);
		_log.LogInformation("Item pickup {ItemId} from {Sender}.", msg.ItemId, sender);
	}
}
