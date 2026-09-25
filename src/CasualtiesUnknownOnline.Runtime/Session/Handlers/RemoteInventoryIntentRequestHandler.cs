using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host native inventory intent. The host is the cross-player
/// authority for this family: it validates the session permission, membership,
/// the ownership fact and the destination body, arbitrates the item id
/// first-writer-wins, and forwards the intent to the player whose real body
/// must execute it. It never models inventory contents.
/// </summary>
[PacketHandler(NetMsg.RemoteInventoryIntentRequest, NetMessageDirection.GuestToHost)]
internal sealed class RemoteInventoryIntentRequestHandler : PacketHandlerBase<RemoteInventoryIntentMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, RemoteInventoryIntentMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.HandleRemoteInventoryIntentRequest(sender, msg);
}
