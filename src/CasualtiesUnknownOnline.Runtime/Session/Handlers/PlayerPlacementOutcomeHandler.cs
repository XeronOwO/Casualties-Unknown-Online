using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Receiving client → host: what its placement really did with a given item — the place it used, or that it
/// placed nothing. The host admits it only from the receiving body it wrote that give's row to, compares the place
/// with its own record, and either settles the give or refuses the outcome by name and returns the row to the
/// giver. A report with no matching committed placement is refused by name and moves nothing: it can neither move
/// an item nor invent a return.
/// </summary>
[PacketHandler(NetMsg.PlayerPlacementOutcome, NetMessageDirection.GuestToHost)]
internal sealed class PlayerPlacementOutcomeHandler : PacketHandlerBase<PlayerPlacementOutcomeMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, PlayerPlacementOutcomeMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.HandlePlacementOutcome(sender, msg);
}
