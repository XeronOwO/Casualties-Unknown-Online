using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host take request (direct player interaction): the host arbitrates
/// the cross-player inventory transfer and sends the authoritative body
/// mutation to the two participants.
/// </summary>
[PacketHandler(NetMsg.PlayerInventoryTakeRequest, NetMessageDirection.GuestToHost)]
internal sealed class PlayerInventoryTakeRequestHandler : PacketHandlerBase<PlayerInventoryTakeRequestMsg>
{
	protected override void Handle(ulong sender, PlayerInventoryTakeRequestMsg msg, HandlerContext ctx) =>
		ctx.PlayerInteraction.HandleTakeRequest(sender, msg);
}
