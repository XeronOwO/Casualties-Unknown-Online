using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// One crafting operation's complete terminal state: guest → host report; host
/// → guest broadcast relay (source excluded). The host classifies each entry
/// against its tables, applies and relays the WHOLE report (CraftSyncService)
/// — never decomposed into per-entry broadcasts, so one-operation-one-report
/// holds end-to-end.
/// </summary>
[PacketHandler(NetMsg.CraftReport)]
public sealed class CraftReportHandler(ILogger<CraftReportHandler> log) : PacketHandlerBase<CraftReportMsg>
{
	private readonly ILogger<CraftReportHandler> _log = log;

	protected override void Handle(ulong sender, CraftReportMsg msg, HandlerContext ctx)
	{
		ctx.Craft.FireCraftReportReceived(sender, msg);
		_log.LogInformation("Craft report ({Kind}) from {Sender}.", msg.Kind, sender);
	}
}
