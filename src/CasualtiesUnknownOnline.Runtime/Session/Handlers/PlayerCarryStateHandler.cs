using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → all authoritative carry-state broadcast (direct player interaction):
/// every side updates its local carry mirror and the Game Adapter sets/clears
/// the carried-body driver.
/// </summary>
[PacketHandler(NetMsg.PlayerCarryState)]
internal sealed class PlayerCarryStateHandler : PacketHandlerBase<PlayerCarryStateMsg>
{
	protected override void Handle(ulong sender, PlayerCarryStateMsg msg, HandlerContext ctx) =>
		ctx.PlayerInteraction.FireCarryStateReceived(msg);
}
