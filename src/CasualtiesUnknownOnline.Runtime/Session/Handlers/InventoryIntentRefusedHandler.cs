using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → requester: the inventory intent this client performed was refused, and this is the host's reason for it.
/// <para>
/// Before this arrived, a refused gesture was indistinguishable from a gesture that silently did nothing: the actor's
/// client had already written its positive capture line, and every refusal lived in the host's log. The line below is
/// the answer the family's own rule requires — a report the receiving side contradicts is answered, so both ends end
/// up agreeing.
/// </para>
/// </summary>
[PacketHandler(NetMsg.InventoryIntentRefused, NetMessageDirection.HostToGuest)]
public sealed class InventoryIntentRefusedHandler(ILogger<InventoryIntentRefusedHandler> log)
	: PacketHandlerBase<PlayerInventoryIntentRefusedMsg, ISessionHandlerContext>
{
	protected override void Handle(ulong sender, PlayerInventoryIntentRefusedMsg msg, ISessionHandlerContext ctx)
	{
		if (ctx.Session.Role != SessionRole.Guest)
		{
			// The wire direction is already enforced at the receiver, so this is the invariant made observable
			// rather than the boundary itself.
			log.LogWarning("[RemoteIntent] a refusal for {Kind} arrived from {Sender} while this process is {Role} — dropped.",
				msg.Kind, sender, ctx.Session.Role);
			return;
		}

		if (sender != ctx.Session.HostSteamId)
		{
			// A host-to-guest frame whose sender is not this session's host is a CLAIM: the family's own
			// sender-anchored rule (the announcement handlers follow it too).
			log.LogWarning("[RemoteIntent] a refusal for {Kind} arrived from {Sender}, which is not this session's host — dropped.",
				msg.Kind, sender);
			return;
		}

		// Warning, not Information: this is the only place the player's own action was contradicted, and the level is
		// what makes the refusal legible next to the positive capture line that preceded it.
		log.LogWarning("[RemoteIntent] the host refused {Kind} on item {Item}: {Reason} — nothing on your side changed.",
			msg.Kind, msg.ItemInstanceId, msg.Reason);
	}
}
