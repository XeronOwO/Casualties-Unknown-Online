using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → guest: the authoritative world-time speed. The receiver applies it
/// through the SAME SetTimeScale path as local speed changes (so the HUD's
/// curTimeScale and sounds update), guarded by the WorldTimeApply call origin.
/// </summary>
[PacketHandler(NetMsg.WorldTime)]
public sealed class WorldTimeHandler(ILogger<WorldTimeHandler> log) : PacketHandlerBase<WorldTimeMsg>
{
	private readonly ILogger<WorldTimeHandler> _log = log;

	protected override void Handle(ulong sender, WorldTimeMsg msg, HandlerContext ctx)
	{
		_log.LogInformation("World-time broadcast: {Speed}.", msg.Speed);
		ctx.WorldTime.FireTimeReceived(msg.Speed);
	}
}
