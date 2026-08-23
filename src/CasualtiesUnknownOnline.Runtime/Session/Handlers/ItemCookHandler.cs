using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The heater cooker's conversion event — one operation = one message.
/// Host → guest only: the host's full-physics scene ran the native conversion
/// (Heater.cs:41-49), updated its authoritative world-item table atomically
/// and broadcasts the complete cooked-steak state; the guest replays the
/// conversion (kill the raw-meat copy, materialize the steak) instead of
/// running its own physics collision — guest world items are layer-isolated
/// to the Ground layer and can never collide with the cooker.
/// </summary>
[PacketHandler(NetMsg.ItemCook, NetMessageDirection.HostToGuest)]
public sealed class ItemCookHandler(ILogger<ItemCookHandler> log) : PacketHandlerBase<ItemCookMsg>
{
	private readonly ILogger<ItemCookHandler> _log = log;

	protected override void Handle(ulong sender, ItemCookMsg msg, HandlerContext ctx)
	{
		ctx.Items.FireItemCookedReceived(sender, msg.SourceItemId, msg.CookedItemId, msg.Item,
			msg.Position.ToNetVector2(), msg.Velocity.ToNetVector2(), msg.Rotation, msg.AngularVelocity);
		_log.LogInformation("Item cook {Source} → {Cooked} ({Type}) from {Sender}.",
			msg.SourceItemId, msg.CookedItemId, msg.Item.ItemId, sender);
	}
}
