using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → requester: the answer to one <c>/give</c> request. The member's verb was
/// applied on the host, so this line is the only thing its console can print — a
/// refusal no one reads is indistinguishable from a command that silently did
/// nothing, which is why the answer is a message rather than a host-side log.
/// </summary>
[PacketHandler(NetMsg.PlayerInventoryGiveResult, NetMessageDirection.HostToGuest)]
internal sealed class PlayerInventoryGiveResultHandler : PacketHandlerBase<PlayerInventoryGiveResultMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, PlayerInventoryGiveResultMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.FireGiveAnswered(msg);
}
