using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host consumable-use request (direct player interaction): the host
/// validates both participants against its authoritative character snapshots,
/// consumes/drains the user's item and sends the authoritative use result to
/// the two participants.
/// </summary>
[PacketHandler(NetMsg.PlayerItemUseRequest, NetMessageDirection.GuestToHost)]
internal sealed class PlayerItemUseRequestHandler : PacketHandlerBase<PlayerItemUseRequestMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, PlayerItemUseRequestMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.HandleUseRequest(sender, msg);
}
