using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Host-only kick path (first admin slice): sends the dedicated
/// <see cref="NetMsg.Kicked"/> to the target, then removes it through the
/// existing session control member-removal surface. Kept as a separate
/// stateless collaborator so <see cref="SessionService"/> stays under the
/// architecture gate — the kick is a session-control-plane behavior, but it
/// does not own session state.
/// </summary>
internal sealed class HostKickService(
	ISessionControl session,
	PacketSender sender,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ILogger _log = log;

	public bool Kick(ulong steamId, string reason)
	{
		if (_session.Role != SessionRole.Host || steamId == _session.LocalSteamId || !_session.TryGetMember(steamId, out _))
		{
			return false;
		}

		_sender.Send(steamId, NetMsg.Kicked, new KickedMsg { Reason = reason }, reliable: true);
		_log.LogWarning("Host kicked member {Member}: {Reason}.", steamId, reason);
		_session.RemoveGuestMember(steamId);
		return true;
	}
}
