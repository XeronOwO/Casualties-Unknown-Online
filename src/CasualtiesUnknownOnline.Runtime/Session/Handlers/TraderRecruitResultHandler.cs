using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host's trader-recruit result arrived (host → the revived player only):
/// apply the authoritative post-revive physiological state to the local Body.
/// </summary>
[PacketHandler(NetMsg.TraderRecruitResult)]
public sealed class TraderRecruitResultHandler(ILogger<TraderRecruitResultHandler> log)
	: PacketHandlerBase<TraderRecruitResultMsg>
{
	private readonly ILogger<TraderRecruitResultHandler> _log = log;

	protected override void Handle(ulong sender, TraderRecruitResultMsg msg, HandlerContext ctx)
	{
		ctx.World.FireTraderRecruitResultReceived(msg);
		_log.LogInformation("[TradeRecruit] result received target={Target} health={Health}.", msg.TargetSteamId, msg.Health?.BrainHealth);
	}
}
