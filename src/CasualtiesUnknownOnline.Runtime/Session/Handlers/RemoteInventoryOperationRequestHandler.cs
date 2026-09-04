using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host remote-backpack inventory operation request (drop / move-into-
/// container / pour). The host is the cross-player inventory authority: it
/// validates the owner/item/container, performs the durable kernel and
/// character-table mutation, and records the participant result event.
/// </summary>
[PacketHandler(NetMsg.RemoteInventoryOperationRequest, NetMessageDirection.GuestToHost)]
internal sealed class RemoteInventoryOperationRequestHandler : PacketHandlerBase<RemoteInventoryOperationRequestMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, RemoteInventoryOperationRequestMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.HandleRemoteInventoryOperation(sender, msg);
}
