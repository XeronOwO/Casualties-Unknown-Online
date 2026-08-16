using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → guest command result (NetMsg.ModCommandResult — Phase 4b). Thin
/// adapter: the mod domain settles the requester's pending callback by
/// ModId + RequestId (unknown ids are dropped with a log).
/// </summary>
[PacketHandler(NetMsg.ModCommandResult)]
public sealed class ModCommandResultHandler(ILogger<ModCommandResultHandler> log) : PacketHandlerBase<ModCommandResultMsg>
{
	private readonly ILogger<ModCommandResultHandler> _log = log;

	protected override void Handle(ulong sender, ModCommandResultMsg msg, HandlerContext ctx)
	{
		ctx.Mods.FireModCommandResultReceived(sender, msg);
		_log.LogInformation("[Mods] {ModId}/{Name} result for {Requester} (request {RequestId}, success {Success}).",
			msg.ModId, msg.Name, sender, msg.RequestId, msg.Success);
	}
}
