using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host: a speed the guest's own client has ALREADY applied (speed
/// hotkey, or the native left/right movement reset). The handler only surfaces
/// the request — the host-side policy (accept-first arbitration, all-unconscious
/// sleep acceleration) lives in the Game Adapter's WorldTimeSync, which answers
/// with a WorldTime broadcast.
/// </summary>
[PacketHandler(NetMsg.WorldTimeRequest, NetMessageDirection.GuestToHost)]
public sealed class WorldTimeRequestHandler(ILogger<WorldTimeRequestHandler> log) : PacketHandlerBase<WorldTimeRequestMsg, IWorldTimeHandlerContext>
{
	private readonly ILogger<WorldTimeRequestHandler> _log = log;

	protected override void Handle(ulong sender, WorldTimeRequestMsg msg, IWorldTimeHandlerContext ctx)
	{
		_log.LogDebug("World-time request from {Sender}: {Speed}.", sender, msg.Speed);
		ctx.WorldTime.FireRequestReceived(sender, msg.Speed);
	}
}
