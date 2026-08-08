using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Scene state, star semantics: guest → host as a report (host tracks the
/// member and relays to the other guests, stamped with the reporter), host →
/// guest as a broadcast. A member leaving the world ends its sync; the host
/// leaving ends the guest's own sync.
/// </summary>
[PacketHandler(NetMsg.SceneState)]
public sealed class SceneStateHandler(SessionService session, ILogger<SceneStateHandler> log)
	: PacketHandlerBase<SceneStateMsg>(session)
{
	private readonly ILogger<SceneStateHandler> _log = log;

	protected override void Handle(ulong sender, SceneStateMsg msg)
	{
		// The reporter is msg.SteamId when the host relays another member's
		// change; the sender itself otherwise (msg.SteamId is stamped by the
		// reporter in SessionService.ReportSceneState).
		var reporter = msg.SteamId != 0 ? msg.SteamId : sender;
		if (!Session.TryGetMember(reporter, out var member))
		{
			return;
		}

		var wasInWorld = member.Entity.InWorld;
		member.Entity.InWorld = msg.State == (byte)SceneStateType.InWorld;
		member.Entity.ReportedSpawnPos = msg.Position.ToNetVector2();

		_log.LogInformation("Peer {Peer} scene state: {State} ({SceneName})", reporter, (SceneStateType)msg.State, msg.SceneName);
		if (wasInWorld != member.Entity.InWorld)
		{
			// Either side pauses when a member leaves the world: the member's
			// state stream stops and the render clone is torn down; re-entering
			// re-activates the same entity.
			if (member.Entity.InWorld)
			{
				Session.FireRemoteSceneChanged(reporter, true);
				if (Session.Role == SessionRole.Host)
				{
					Session.MaybeStartEntitySync();
					Session.BroadcastExcept(reporter, NetMsg.SceneState, msg); // relay: the other guests track the member too
				}
			}
			else
			{
				if (Session.Role == SessionRole.Host)
				{
					Session.EndMemberSync(member);
					Session.BroadcastExcept(reporter, NetMsg.SceneState, msg); // relay
				}
				else if (reporter == Session.HostSteamId)
				{
					Session.EndEntitySync(); // the host left the world — our sync ends
				}

				Session.FireRemoteSceneChanged(reporter, false);
			}
		}
	}
}
