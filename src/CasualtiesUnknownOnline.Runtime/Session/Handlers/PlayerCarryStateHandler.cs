using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → all authoritative carry-state broadcast (direct player interaction):
/// every side updates its local carry mirror and the Game Adapter sets/clears
/// the carried-body driver.
/// </summary>
[PacketHandler(NetMsg.PlayerCarryState, NetMessageDirection.HostToGuest)]
internal sealed class PlayerCarryStateHandler : PacketHandlerBase<PlayerCarryStateMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, PlayerCarryStateMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.FireCarryStateReceived(msg);
}
