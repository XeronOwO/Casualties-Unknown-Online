using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host carry-stop request (direct player interaction): the host
/// clears the current carrier/one carried relation and broadcasts the
/// authoritative empty carry state.
/// </summary>
[PacketHandler(NetMsg.PlayerCarryStopRequest, NetMessageDirection.GuestToHost)]
internal sealed class PlayerCarryStopRequestHandler : PacketHandlerBase<PlayerCarryStopRequestMsg>
{
	protected override void Handle(ulong sender, PlayerCarryStopRequestMsg msg, HandlerContext ctx) =>
		ctx.PlayerInteraction.HandleCarryStopRequest(sender, msg);
}
