using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Scene state, star semantics: guest → host as a report (host tracks the
/// member and relays to the other guests, stamped with the reporter), host →
/// guest as a broadcast. A member leaving the world ends its sync (entity
/// domain); the host leaving ends the guest's own sync.
/// </summary>
[PacketHandler(NetMsg.SceneState)]
public sealed class SceneStateHandler(ILogger<SceneStateHandler> log) : PacketHandlerBase<SceneStateMsg>
{
	private readonly ILogger<SceneStateHandler> _log = log;

	protected override void Handle(ulong sender, SceneStateMsg msg, HandlerContext ctx)
	{
		var session = ctx.Session;
		// The reporter is msg.SteamId when the host relays another member's
		// change; the sender itself otherwise (msg.SteamId is stamped by the
		// reporter in SessionService.ReportSceneState).
		var reporter = msg.SteamId != 0 ? msg.SteamId : sender;
		if (!session.TryGetMember(reporter, out var member))
		{
			return;
		}

		var wasInWorld = member.InWorld;
		member.InWorld = msg.State == (byte)SceneStateType.InWorld;
		member.ReportedSpawnPos = msg.Position.ToNetVector2();

		_log.LogInformation("Peer {Peer} scene state: {State} ({SceneName})", reporter, (SceneStateType)msg.State, msg.SceneName);
		if (wasInWorld != member.InWorld)
		{
			// Either side pauses when a member leaves the world: the member's
			// state stream stops and the render clone is torn down; re-entering
			// re-activates the same entity.
			if (member.InWorld)
			{
				session.FireRemoteSceneChanged(reporter, true);
				if (session.Role == SessionRole.Host)
				{
					ctx.Entities.MaybeStartEntitySync();
					// Re-entering the world (death → menu → re-enter) — hand the
					// saved character data back; the handshake restore only covers
					// reconnects. The save belongs to the CURRENT run: a new run
					// clears the save table at the host's click (RunCoordinator →
					// CharacterDataSync), so this hands back nothing on a fresh
					// run — its starting supplies stay ("started paradise, got
					// the previous run's emergency light" is gone).
					ctx.CharacterData.SendSavedCharacter(reporter);
					// The full world-state backfill (block damage, trap
					// consumptions, opened entities, trap layout, world items) —
					// see HandlerContext.SendWorldStateToMember.
					ctx.SendWorldStateToMember(reporter);
					// Start gate: everyone enters together — or, if the game
					// already started, let this late joiner pass directly.
					ctx.World.NotifyMemberInWorld(reporter);
					session.BroadcastExcept(reporter, NetMsg.SceneState, msg); // relay: the other guests track the member too
				}
			}
			else
			{
				if (session.Role == SessionRole.Host)
				{
					ctx.Entities.EndMemberSync(reporter);
					session.BroadcastExcept(reporter, NetMsg.SceneState, msg); // relay
				}
				else if (reporter == session.HostSteamId)
				{
					ctx.Entities.EndEntitySync(); // the host left the world — our sync ends
				}

				session.FireRemoteSceneChanged(reporter, false);
			}
		}
	}
}
