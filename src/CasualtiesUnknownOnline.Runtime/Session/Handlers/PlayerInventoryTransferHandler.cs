using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → participant transfer result (direct player interaction): the Game
/// Adapter applies the local body mutation (remove from FromSteamId, add to
/// ToSteamId) and immediately re-reports the character snapshot.
/// </summary>
[PacketHandler(NetMsg.PlayerInventoryTransfer, NetMessageDirection.HostToGuest)]
internal sealed class PlayerInventoryTransferHandler : PacketHandlerBase<PlayerInventoryTransferMsg>
{
	protected override void Handle(ulong sender, PlayerInventoryTransferMsg msg, HandlerContext ctx) =>
		ctx.PlayerInteraction.FireTransferReceived(msg);
}
