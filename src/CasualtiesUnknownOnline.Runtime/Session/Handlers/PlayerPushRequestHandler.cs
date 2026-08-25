using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host push request (direct player interaction): the host validates
/// both participants, the distance/cooldown and the pusher's standing against
/// its authoritative entity/character state, then broadcasts the authoritative
/// push result.
/// </summary>
[PacketHandler(NetMsg.PlayerPushRequest, NetMessageDirection.GuestToHost)]
internal sealed class PlayerPushRequestHandler : PacketHandlerBase<PlayerPushRequestMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, PlayerPushRequestMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.HandlePushRequest(sender, msg);
}
