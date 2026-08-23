using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host's physics moved world items — apply the authoritative positions
/// to the local copies, so both sides see the items at the same spot. Host →
/// guest only (direction-validated by PacketReceiver).
/// </summary>
[PacketHandler(NetMsg.ItemMove, NetMessageDirection.HostToGuest)]
public sealed class ItemMoveHandler : PacketHandlerBase<ItemMoveMsg, IItemHandlerContext>
{
	protected override void Handle(ulong sender, ItemMoveMsg msg, IItemHandlerContext ctx) =>
		ctx.Items.FireItemMoveReceived(msg.Items);
}
