using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The spectator notice through the star network. At the host it is a watcher's report: the host checks that the
/// claimed watcher IS the transport sender (a member cannot report on another member's behalf), fires the local
/// event and relays the same payload to the watched member alone. At the watched member it is the host's relay: the
/// notice is surfaced locally and never re-relayed.
/// </summary>
[PacketHandler(NetMsg.SpectatorNotice, NetMessageDirection.Bidirectional)]
public sealed class SpectatorNoticeHandler(ILogger<SpectatorNoticeHandler> log)
	: PacketHandlerBase<SpectatorNoticeMsg, IWorldSessionHandlerContext>
{
	private readonly ILogger<SpectatorNoticeHandler> _log = log;

	protected override void Handle(ulong sender, SpectatorNoticeMsg msg, IWorldSessionHandlerContext ctx)
	{
		if (msg.WatcherSteamId == 0 || msg.TargetSteamId == 0 || msg.WatcherSteamId == msg.TargetSteamId)
		{
			_log.LogWarning("[Spectator] dropping malformed notice watcher={Watcher} target={Target}.", msg.WatcherSteamId, msg.TargetSteamId);
			return;
		}

		// The host's own receipt: the watcher must be the reporting member. The relay the host sends carries the host
		// as transport sender, so this check belongs to the receipt only.
		if (ctx.Session.Role == SessionRole.Host && msg.WatcherSteamId != sender)
		{
			_log.LogWarning("[Spectator] dropping spoofed notice transport={Transport} claimed={Claimed}.", sender, msg.WatcherSteamId);
			return;
		}

		ctx.World.FireSpectatorNoticeReceived(sender, msg);

		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.World.RelaySpectatorNotice(msg);
		}

		_log.LogInformation("[Spectator] notice watcher={Watcher} target={Target} watching={Watching} role={Role}.",
			msg.WatcherSteamId, msg.TargetSteamId, msg.Watching, ctx.Session.Role);
	}
}
