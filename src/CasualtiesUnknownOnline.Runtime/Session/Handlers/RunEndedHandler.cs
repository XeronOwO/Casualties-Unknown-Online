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

		// The broadcast is a control message for ONE run. A late one (measured 2026-11-14: a
		// run-0 broadcast reached a member that had just entered run 1) must not end the newer
		// run it no longer names. The host now always names its current run; 0 is a legacy
		// unscoped value and is ignored whenever this side knows which run it is in.
		if (ctx.World.CurrentGeneration is { } generation)
		{
			if (msg.RunId == 0 || generation.RunEpoch != (ulong)msg.RunId)
			{
				_log.LogInformation(
					"[RunEnd] ignoring the run-end for run {RunId}: this member is in run {CurrentRunId}.",
					msg.RunId,
					generation.RunEpoch);
				return;
			}
		}

		_log.LogInformation("[RunEnd] the host's world ended (run {RunId}) — leaving it.", msg.RunId);
		ctx.World.FireRunEndedReceived(msg);
	}
}
