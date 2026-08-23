using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → guest: the host removed this member from the session. The guest
/// tears its own session down immediately; the host keeps running and other
/// members are untouched (the entity domain already broadcasts PlayerLeave for
/// the removed member through the host-side MemberRemoved path).
/// </summary>
[PacketHandler(NetMsg.Kicked, NetMessageDirection.HostToGuest)]
public sealed class KickedHandler(ILogger<KickedHandler> log) : PacketHandlerBase<KickedMsg, ISessionHandlerContext>
{
	private readonly ILogger<KickedHandler> _log = log;

	protected override void Handle(ulong sender, KickedMsg msg, ISessionHandlerContext ctx)
	{
		var session = ctx.Session;
		if (session.Role != SessionRole.Guest)
		{
			return;
		}

		_log.LogWarning("Kicked by the host ({Reason}) — ending session.", msg.Reason);
		session.EndSession();
	}
}
