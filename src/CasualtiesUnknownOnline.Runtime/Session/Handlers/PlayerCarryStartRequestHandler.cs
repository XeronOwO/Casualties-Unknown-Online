using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host carry-start request (direct player interaction): the host
/// validates the carryable state, records the one carrier/one carried relation
/// and broadcasts the authoritative carry state.
/// </summary>
[PacketHandler(NetMsg.PlayerCarryStartRequest, NetMessageDirection.GuestToHost)]
internal sealed class PlayerCarryStartRequestHandler : PacketHandlerBase<PlayerCarryStartRequestMsg>
{
	protected override void Handle(ulong sender, PlayerCarryStartRequestMsg msg, HandlerContext ctx) =>
		ctx.PlayerInteraction.HandleCarryStartRequest(sender, msg);
}
