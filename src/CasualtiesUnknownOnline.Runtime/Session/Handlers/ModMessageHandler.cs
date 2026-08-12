using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A mod frame arrived (NetMsg.ModMessage — Phase 4 Mod API). No auto-relay:
/// the frame already carries the sender and the destination direction (a guest
/// sent it as a report to the host; the host sent it as a directed/broadcast
/// frame) — star topology, the frame is routed to the local mod with the
/// carried id, unknown ids are dropped with a log.
/// </summary>
[PacketHandler(NetMsg.ModMessage)]
public sealed class ModMessageHandler(ILogger<ModMessageHandler> log) : PacketHandlerBase<ModMessageMsg>
{
	private readonly ILogger<ModMessageHandler> _log = log;

	protected override void Handle(ulong sender, ModMessageMsg msg, HandlerContext ctx)
	{
		ctx.Mods.FireModMessageReceived(sender, msg);
		_log.LogInformation("[Mods] {Sender} → {ModId} ({Length} bytes).", sender, msg.ModId, msg.Payload.Length);
	}
}
