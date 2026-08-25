using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → all push result (direct player interaction): surface the
/// authoritative result for the Game Adapter to apply the local target body
/// mutation/pusher cost and to play the one-shot push sound.
/// </summary>
[PacketHandler(NetMsg.PlayerPushResult, NetMessageDirection.HostToGuest)]
internal sealed class PlayerPushResultHandler : PacketHandlerBase<PlayerPushResultMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, PlayerPushResultMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.FirePushReceived(msg);
}
