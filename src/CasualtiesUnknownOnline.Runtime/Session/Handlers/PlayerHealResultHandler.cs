using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → participant(s) heal result (direct player interaction): surface the
/// authoritative result for the Game Adapter to apply the local body mutation
/// (consume the healer's item and/or apply the target's post-heal state).
/// </summary>
[PacketHandler(NetMsg.PlayerHealResult, NetMessageDirection.HostToGuest)]
internal sealed class PlayerHealResultHandler : PacketHandlerBase<PlayerHealResultMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, PlayerHealResultMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.FireHealReceived(msg);
}
