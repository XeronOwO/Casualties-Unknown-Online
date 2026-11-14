using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host's run end through the star network. At a member it is the host's broadcast: the member's own
/// leave path runs while the world still exists, so its scene unloads with the player's exit instead of
/// standing there with no host to play with. At the host it never arrives — the host is the sender.
///
/// Measured 2026-11-14: after the host left, the member's page read Home while its state still said in
/// world, and the host's clone kept updating under its inventory slots.
/// </summary>
[PacketHandler(NetMsg.RunEnded, NetMessageDirection.HostToGuest)]
public sealed class RunEndedHandler(ILogger<RunEndedHandler> log)
	: PacketHandlerBase<RunEndedMsg, IWorldSessionHandlerContext>
{
	private readonly ILogger<RunEndedHandler> _log = log;

	protected override void Handle(ulong sender, RunEndedMsg msg, IWorldSessionHandlerContext ctx)
	{
		if (ctx.Session.Role == SessionRole.Host)
		{
			_log.LogWarning("[RunEnd] a run-end broadcast arrived at its own host; ignored.");
			return;
		}

		_log.LogInformation("[RunEnd] the host's world ended (run {RunId}) — leaving it.", msg.RunId);
		ctx.World.FireRunEndedReceived(msg);
	}
}
