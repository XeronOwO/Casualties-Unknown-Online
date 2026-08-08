using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest side: self-activation (the host assigned our id) or a roster
/// announcement (another member joined — upsert with its spawn anchor).
/// </summary>
[PacketHandler(NetMsg.PlayerJoin)]
public sealed class PlayerJoinHandler(SessionService session, ILogger<PlayerJoinHandler> log)
	: PacketHandlerBase<PlayerJoinMsg>(session)
{
	private readonly ILogger<PlayerJoinHandler> _log = log;

	protected override void Handle(ulong sender, PlayerJoinMsg msg)
	{
		if (Session.Role != SessionRole.Guest)
		{
			return;
		}

		if (msg.GuestSteamId == Session.LocalSteamId)
		{
			Session.LocalPlayer.EntityId = msg.GuestEntityId.ToNetworkEntityId();
			var host = Session.GetOrCreateMember(msg.HostSteamId);
			host.Entity.SteamId = msg.HostSteamId; // backfill (session already knows it)
			host.Entity.EntityId = msg.HostEntityId.ToNetworkEntityId();
			host.Entity.Position = msg.HostPosition.ToNetVector2();
			Session.SetEntitySyncActive(true);
			Session.ResetLastStateSeq(); // host's snapshot sequence restarts with this join
			_log.LogInformation("PlayerJoin received: local {Local}, host {Host} at {Position}.",
				Session.LocalPlayer.EntityId, host.Entity.EntityId, host.Entity.Position);
			Session.FireRemoteJoined(host.Entity);
		}
		else
		{
			var member = Session.GetOrCreateMember(msg.GuestSteamId);
			member.Entity.EntityId = msg.GuestEntityId.ToNetworkEntityId();
			member.Entity.Position = msg.GuestPosition.ToNetVector2();
			member.Entity.InWorld = true;
			member.Handshaken = true;
			_log.LogInformation("Roster join: member {Guest} ({GuestId}) at {Position}.",
				msg.GuestSteamId, member.Entity.EntityId, member.Entity.Position);
			Session.FireRemoteJoined(member.Entity);
		}
	}
}
