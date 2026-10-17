using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host: a member typed <c>/give</c>. The host judges its own cheat switch
/// and roster, creates the item from its own definition table and instance-id
/// space, delivers it through the ordinary inventory-transfer custody path, and
/// answers with one sentence — the answer the member's console prints.
/// </summary>
[PacketHandler(NetMsg.PlayerInventoryGiveRequest, NetMessageDirection.GuestToHost)]
internal sealed class PlayerInventoryGiveRequestHandler : PacketHandlerBase<PlayerInventoryGiveRequestMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, PlayerInventoryGiveRequestMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.HandleGiveRequest(sender, msg);
}
