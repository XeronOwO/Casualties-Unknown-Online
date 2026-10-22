using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Receiving client → host: the answered destination cannot be used any more, so the row must come back. The
/// host admits it only for a placement it committed to THAT client and for an item that client still holds; it
/// then returns the row and publishes the return. A report with no matching committed placement is dropped by
/// name — it can neither move an item nor invent a return.
/// </summary>
[PacketHandler(NetMsg.PlayerPlacementFailed, NetMessageDirection.GuestToHost)]
internal sealed class PlayerPlacementFailedHandler : PacketHandlerBase<PlayerPlacementFailedMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, PlayerPlacementFailedMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.HandlePlacementFailed(sender, msg);
}
