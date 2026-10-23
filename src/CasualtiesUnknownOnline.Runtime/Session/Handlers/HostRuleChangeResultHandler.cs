using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → requester: the answer to one <c>/gamerule</c> request. The member's verb was
/// applied on the host (or refused there), so this line is the only thing its console can
/// print — a refusal nobody reads is indistinguishable from a command that silently did
/// nothing.
///
/// <para>
/// The sender is part of the verdict: the wire direction check proves only this client's
/// role, so a frame whose sender is not this session's host is a CLAIM and is dropped by
/// name — a forged answer would print attacker-chosen text on an operator's console.
/// </para>
/// </summary>
[PacketHandler(NetMsg.HostRuleChangeResult, NetMessageDirection.HostToGuest)]
internal sealed class HostRuleChangeResultHandler(ILogger<HostRuleChangeResultHandler> log)
	: PacketHandlerBase<HostRuleChangeResultMsg, IHostRuleChangeHandlerContext>
{
	protected override void Handle(ulong sender, HostRuleChangeResultMsg msg, IHostRuleChangeHandlerContext ctx)
	{
		if (sender != ctx.Session.HostSteamId)
		{
			log.LogWarning("[Rules] a change answer arrived from {Sender}, which is not this session's host — dropped.", sender);
			return;
		}

		ctx.HostRuleChanges.FireAnswered(msg);
	}
}
