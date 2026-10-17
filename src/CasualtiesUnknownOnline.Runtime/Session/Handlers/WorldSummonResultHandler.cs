using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → requester: the answer to one <c>/summon</c> request. The member's verb was applied on the host, so this
/// line is the only thing its console can print — a refusal no one reads is indistinguishable from a command
/// that silently did nothing, which is why the answer is a message rather than a host-side log.
///
/// <para>
/// The sender is part of the verdict: the wire direction check proves only this client's role, so a frame whose
/// sender is not this session's host is a CLAIM and is dropped by name — the rule the give answer and the
/// announcement handlers follow, because a forged answer would print attacker-chosen text on an operator's
/// console.
/// </para>
/// </summary>
[PacketHandler(NetMsg.WorldSummonResult, NetMessageDirection.HostToGuest)]
internal sealed class WorldSummonResultHandler(ILogger<WorldSummonResultHandler> log)
	: PacketHandlerBase<WorldSummonResultMsg, IWorldSummonHandlerContext>
{
	protected override void Handle(ulong sender, WorldSummonResultMsg msg, IWorldSummonHandlerContext ctx)
	{
		if (sender != ctx.Session.HostSteamId)
		{
			log.LogWarning("[Summon] an answer for {Prefab} arrived from {Sender}, which is not this session's host — dropped.",
				msg.PrefabId, sender);
			return;
		}

		ctx.WorldSummon.FireSummonAnswered(msg);
	}
}
