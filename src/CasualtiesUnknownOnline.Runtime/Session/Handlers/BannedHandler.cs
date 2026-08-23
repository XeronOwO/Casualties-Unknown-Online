using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → guest: the host banned this member. The guest tears its own session
/// down immediately; unlike a plain kick, the host also persists the SteamID,
/// so the same player cannot handshake into this host again until unbanned.
/// </summary>
[PacketHandler(NetMsg.Banned, NetMessageDirection.HostToGuest)]
public sealed class BannedHandler(ILogger<BannedHandler> log) : PacketHandlerBase<BannedMsg, ISessionHandlerContext>
{
	private readonly ILogger<BannedHandler> _log = log;

	protected override void Handle(ulong sender, BannedMsg msg, ISessionHandlerContext ctx)
	{
		var session = ctx.Session;
		if (session.Role != SessionRole.Guest)
		{
			return;
		}

		_log.LogWarning("Banned by the host ({Reason}) — ending session.", msg.Reason);
		session.EndSession();
	}
}
