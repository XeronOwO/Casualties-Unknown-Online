using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → owner remote-backpack native-operation application. The host already
/// validated the request; the owner's Game Adapter receives this one-shot
/// instruction and executes the exact native body/item operation on its own
/// real body (never on a remote display proxy).
/// </summary>
[PacketHandler(NetMsg.RemoteInventoryApply, NetMessageDirection.HostToGuest)]
internal sealed class RemoteInventoryApplyHandler : PacketHandlerBase<RemoteInventoryApplyMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, RemoteInventoryApplyMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.FireRemoteInventoryApplyReceived(msg);
}
