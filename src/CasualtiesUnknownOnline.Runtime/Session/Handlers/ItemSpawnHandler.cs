using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A runtime-generated item entered the world: guest → host as a report (the
/// host registers it in the authoritative table and relays to the other
/// guests, source excluded), host → guest as a broadcast relay. The item state
/// travels with the message so every receiver can materialize the object.
/// </summary>
[PacketHandler(NetMsg.ItemSpawn)]
public sealed class ItemSpawnHandler(ILogger<ItemSpawnHandler> log) : PacketHandlerBase<ItemSpawnMsg>
{
	private readonly ILogger<ItemSpawnHandler> _log = log;

	protected override void Handle(ulong sender, ItemSpawnMsg msg, HandlerContext ctx)
	{
		ctx.Items.FireItemSpawnedReceived(sender, msg.ItemId, msg.Item, msg.Position.ToNetVector2(), msg.Velocity.ToNetVector2());
		_log.LogInformation("Item spawn {ItemId} ({Type}) from {Sender}.", msg.ItemId, msg.Item.ItemId, sender);
	}
}
