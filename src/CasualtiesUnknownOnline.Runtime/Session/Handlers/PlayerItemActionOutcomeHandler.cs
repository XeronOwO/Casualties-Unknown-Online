using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Affected side → host outcome of a cross-player use the host cannot compute
/// (direct player interaction): the item's own action ran on this client against
/// its own body or limb and its own copy of another member's carried item, and
/// this is what the item became. The host matches it against the use it admitted
/// for this pair, commits the state into the owner's authoritative record and
/// publishes the ordinary use result. The two families that report here — the
/// solid-food eat and the limb-tool use — are
/// <see cref="PlayerItemActionOutcomeMsg"/>'s.
/// </summary>
[PacketHandler(NetMsg.PlayerItemActionOutcome, NetMessageDirection.GuestToHost)]
internal sealed class PlayerItemActionOutcomeHandler : PacketHandlerBase<PlayerItemActionOutcomeMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, PlayerItemActionOutcomeMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.HandleItemActionOutcome(sender, msg);
}
