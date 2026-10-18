using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → a member target's client: apply the full heal reset to your own body. The sender is part
/// of the verdict — the wire direction check proves only this client's role, so a frame whose
/// sender is not this session's host is a CLAIM and is dropped by name, the rule every
/// host-addressed instruction follows.
/// </summary>
[PacketHandler(NetMsg.HealResetInstruction, NetMessageDirection.HostToGuest)]
internal sealed class HealResetInstructionHandler(ILogger<HealResetInstructionHandler> log)
	: PacketHandlerBase<HealResetInstructionMsg, IHealResetHandlerContext>
{
	protected override void Handle(ulong sender, HealResetInstructionMsg msg, IHealResetHandlerContext ctx)
	{
		if (sender != ctx.Session.HostSteamId)
		{
			log.LogWarning("[HealReset] a reset instruction from {Sender}, which is not this session's host — dropped.", sender);
			return;
		}

		ctx.HealReset.HandleInstruction(sender, msg);
	}
}
