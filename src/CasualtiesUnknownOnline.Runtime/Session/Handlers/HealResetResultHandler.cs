using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → requester: the answer to one <c>/heal</c> request. The member's verb was judged on the
/// host, so this line is the only thing its console can print — a refusal no one reads is
/// indistinguishable from a command that silently did nothing. The sender is part of the verdict: a
/// frame whose sender is not this session's host is a claim and is dropped by name.
/// </summary>
[PacketHandler(NetMsg.HealResetResult, NetMessageDirection.HostToGuest)]
internal sealed class HealResetResultHandler(ILogger<HealResetResultHandler> log)
	: PacketHandlerBase<HealResetResultMsg, IHealResetHandlerContext>
{
	protected override void Handle(ulong sender, HealResetResultMsg msg, IHealResetHandlerContext ctx)
	{
		if (sender != ctx.Session.HostSteamId)
		{
			log.LogWarning("[HealReset] an answer arrived from {Sender}, which is not this session's host — dropped.", sender);
			return;
		}

		ctx.HealReset.FireAnswered(msg);
	}
}
