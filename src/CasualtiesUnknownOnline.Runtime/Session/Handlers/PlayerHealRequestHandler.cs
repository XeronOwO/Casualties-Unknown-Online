using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host heal request (direct player interaction): the host validates
/// both participants against its authoritative character snapshots, consumes
/// the healer's medical item and sends the authoritative heal result to the two
/// participants.
/// </summary>
[PacketHandler(NetMsg.PlayerHealRequest, NetMessageDirection.GuestToHost)]
internal sealed class PlayerHealRequestHandler : PacketHandlerBase<PlayerHealRequestMsg>
{
	protected override void Handle(ulong sender, PlayerHealRequestMsg msg, HandlerContext ctx) =>
		ctx.PlayerInteraction.HandleHealRequest(sender, msg);
}
