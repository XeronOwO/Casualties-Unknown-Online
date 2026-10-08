using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Eater → host solid-food outcome (direct player interaction): the eat ran on
/// this client against its own body and its own copy of another member's carried
/// item, and this is the item's post-eat condition. The host matches it against
/// the eat it admitted for this pair, commits the state into the owner's
/// authoritative record and publishes the ordinary use result.
/// </summary>
[PacketHandler(NetMsg.PlayerItemEatOutcome, NetMessageDirection.GuestToHost)]
internal sealed class PlayerItemEatOutcomeHandler : PacketHandlerBase<PlayerItemEatOutcomeMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, PlayerItemEatOutcomeMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.HandleItemEatOutcome(sender, msg);
}
