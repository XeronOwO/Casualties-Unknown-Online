using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Host → guest: a synced member left — drop it (clone teardown via RemoteSceneChanged).</summary>
[PacketHandler(NetMsg.PlayerLeave, NetMessageDirection.HostToGuest)]
public sealed class PlayerLeaveHandler(ILogger<PlayerLeaveHandler> log) : PacketHandlerBase<PlayerLeaveMsg, ISessionHandlerContext>
{
	private readonly ILogger<PlayerLeaveHandler> _log = log;

	protected override void Handle(ulong sender, PlayerLeaveMsg msg, ISessionHandlerContext ctx)
	{
		var session = ctx.Session;
		if (session.Role != SessionRole.Guest || msg.SteamId == session.LocalSteamId)
		{
			return;
		}

		if (!session.TryGetMember(msg.SteamId, out _))
		{
			return; // unknown member: nothing to drop
		}

		_log.LogInformation("Member {Member} left (PlayerLeave).", msg.SteamId);
		session.RemoveGuestMember(msg.SteamId);
	}
}
