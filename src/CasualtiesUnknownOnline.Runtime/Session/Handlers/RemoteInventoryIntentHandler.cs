using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → owner native inventory intent. The host already validated the
/// request; the owner's Game Adapter receives this one-shot instruction and
/// replays the exact native call on its own real items (never on a remote
/// display proxy), then reports the result through the existing authoritative
/// item and character paths.
/// </summary>
[PacketHandler(NetMsg.RemoteInventoryIntent, NetMessageDirection.HostToGuest)]
internal sealed class RemoteInventoryIntentHandler : PacketHandlerBase<RemoteInventoryIntentMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, RemoteInventoryIntentMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.FireRemoteInventoryIntentReceived(msg);
}
