using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A guest's trader-recruit request arrived (the acting side already located
/// its nearest trader) — the host validates the trade gates and the dead
/// player, then sends the authoritative revive result to the target. The
/// request is a dedicated message: unlike ordinary trade actions there is no
/// vanilla game method for the acting side to run, so the host decides the
/// entire outcome.
/// </summary>
[PacketHandler(NetMsg.TraderRecruitRequest, NetMessageDirection.GuestToHost)]
public sealed class TraderRecruitRequestHandler(ILogger<TraderRecruitRequestHandler> log)
	: PacketHandlerBase<TraderRecruitRequestMsg, IWorldHandlerContext>
{
	private readonly ILogger<TraderRecruitRequestHandler> _log = log;

	protected override void Handle(ulong sender, TraderRecruitRequestMsg msg, IWorldHandlerContext ctx)
	{
		ctx.World.FireTraderRecruitRequestReceived(sender, msg);
		_log.LogInformation("[TradeRecruit] request received from={Sender} target={Target} trader=({X:0.0},{Y:0.0}).",
			sender, msg.TargetSteamId, msg.TraderPosition.X, msg.TraderPosition.Y);
	}
}
