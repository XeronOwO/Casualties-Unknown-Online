using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host: the guest's speed hotkey / movement-reset intent. The
/// handler only surfaces the request — the host-side policy (movement gate,
/// all-unconscious sleep acceleration) lives in the Game Adapter's
/// WorldTimeSync, which answers with a WorldTime broadcast.
/// </summary>
[PacketHandler(NetMsg.WorldTimeRequest)]
public sealed class WorldTimeRequestHandler(ILogger<WorldTimeRequestHandler> log) : PacketHandlerBase<WorldTimeRequestMsg>
{
	private readonly ILogger<WorldTimeRequestHandler> _log = log;

	protected override void Handle(ulong sender, WorldTimeRequestMsg msg, HandlerContext ctx)
	{
		_log.LogInformation("World-time request from {Sender}: {Speed}.", sender, msg.Speed);
		ctx.WorldTime.FireRequestReceived(sender, msg.Speed);
	}
}
