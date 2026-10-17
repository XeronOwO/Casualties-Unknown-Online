using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → requester: the answer to one <c>/give</c> request. The member's verb was
/// applied on the host, so this line is the only thing its console can print — a
/// refusal no one reads is indistinguishable from a command that silently did
/// nothing, which is why the answer is a message rather than a host-side log.
///
/// <para>
/// The sender is part of the verdict: the wire direction check proves only this
/// client's role, so a frame whose sender is not this session's host is a CLAIM and
/// is dropped by name — the rule the announcement handlers follow, because a forged
/// answer would print attacker-chosen text on an operator's console.
/// </para>
/// </summary>
[PacketHandler(NetMsg.PlayerInventoryGiveResult, NetMessageDirection.HostToGuest)]
internal sealed class PlayerInventoryGiveResultHandler(ILogger<PlayerInventoryGiveResultHandler> log)
	: PacketHandlerBase<PlayerInventoryGiveResultMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, PlayerInventoryGiveResultMsg msg, IPlayerInteractionHandlerContext ctx)
	{
		if (sender != ctx.Session.HostSteamId)
		{
			log.LogWarning("[Give] an answer for {Definition} arrived from {Sender}, which is not this session's host — dropped.",
				msg.DefinitionId, sender);
			return;
		}

		ctx.PlayerInteraction.FireGiveAnswered(msg);
	}
}
