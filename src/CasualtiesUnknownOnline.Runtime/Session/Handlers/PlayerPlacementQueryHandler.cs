using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → receiving client: where would this give land on your body? The receiver answers with its own rules;
/// nothing has been written on either side yet, which is what keeps an item the destination cannot take with the
/// giver (the user's 2026-10-22 ruling). The handler's context owns the sender check — the wire direction only
/// proves the local role, so a question from anyone but the session's host is dropped by name.
/// </summary>
[PacketHandler(NetMsg.PlayerPlacementQuery, NetMessageDirection.HostToGuest)]
internal sealed class PlayerPlacementQueryHandler : PacketHandlerBase<PlayerPlacementQueryMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, PlayerPlacementQueryMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.HandlePlacementQuery(sender, msg);
}
