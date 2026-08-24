using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → participant(s) consumable-use result (direct player interaction):
/// surface the authoritative result for the Game Adapter to apply the local body
/// mutation (consume/update the user's item and/or apply the target's post-use
/// state).
/// </summary>
[PacketHandler(NetMsg.PlayerItemUseResult, NetMessageDirection.HostToGuest)]
internal sealed class PlayerItemUseResultHandler : PacketHandlerBase<PlayerItemUseResultMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, PlayerItemUseResultMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.FireUseReceived(msg);
}
