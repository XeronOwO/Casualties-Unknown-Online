using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// An item left a player's inventory/container into the world: guest → host as
/// a report (the host registers it in the authoritative table and relays,
/// source excluded), host → guest as a broadcast relay. Receivers without the
/// item materialize it at the carried position.
/// </summary>
[PacketHandler(NetMsg.ItemDrop, NetMessageDirection.Bidirectional)]
public sealed class ItemDropHandler(ILogger<ItemDropHandler> log) : PacketHandlerBase<ItemDropMsg>
{
	private readonly ILogger<ItemDropHandler> _log = log;

	protected override void Handle(ulong sender, ItemDropMsg msg, HandlerContext ctx)
	{
		ctx.Items.FireItemDroppedReceived(sender, msg.ItemId, msg.Item, msg.Position.ToNetVector2(),
			msg.Velocity?.ToNetVector2() ?? NetVector2.Zero, msg.ParentItemId, msg.Rotation, msg.AngularVelocity,
			msg.ParentPosition?.ToNetVector2() ?? NetVector2.Zero);
		_log.LogInformation("Item drop {ItemId} ({Type}) from {Sender}.", msg.ItemId, msg.Item.ItemId, sender);
	}
}
