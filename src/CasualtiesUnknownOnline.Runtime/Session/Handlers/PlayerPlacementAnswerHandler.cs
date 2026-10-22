using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Receiving client → host: the placement this client's own rules resolved, or the reason it refuses. The host
/// commits the ANSWERED destination and nothing else; an answer to a question it did not ask (already answered,
/// timed out, or from a previous session) is dropped by name.
/// </summary>
[PacketHandler(NetMsg.PlayerPlacementAnswer, NetMessageDirection.GuestToHost)]
internal sealed class PlayerPlacementAnswerHandler : PacketHandlerBase<PlayerPlacementAnswerMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, PlayerPlacementAnswerMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.HandlePlacementAnswer(sender, msg);
}
