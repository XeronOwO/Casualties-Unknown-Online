using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Host → guest: a synced member left — drop it (clone teardown via RemoteSceneChanged).</summary>
[PacketHandler(NetMsg.PlayerLeave)]
public sealed class PlayerLeaveHandler(SessionService session, ILogger<PlayerLeaveHandler> log)
	: PacketHandlerBase<PlayerLeaveMsg>(session)
{
	private readonly ILogger<PlayerLeaveHandler> _log = log;

	protected override void Handle(ulong sender, PlayerLeaveMsg msg)
	{
		if (Session.Role != SessionRole.Guest || msg.SteamId == Session.LocalSteamId)
		{
			return;
		}

		if (!Session.TryGetMember(msg.SteamId, out _))
		{
			return; // unknown member: nothing to drop
		}

		_log.LogInformation("Member {Member} left (PlayerLeave).", msg.SteamId);
		Session.RemoveGuestMember(msg.SteamId);
	}
}
