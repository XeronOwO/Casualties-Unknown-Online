using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Scene state, star semantics: guest → host as a report (host tracks the
/// member and relays to the other guests, stamped with the reporter), host →
/// guest as a broadcast. A member leaving the world ends its sync (entity
/// domain); the host leaving ends the guest's own sync.
/// </summary>
[PacketHandler(NetMsg.SceneState, NetMessageDirection.Bidirectional)]
public sealed class SceneStateHandler(ILogger<SceneStateHandler> log, WorldEntryFanout worldEntryFanout, ITimeSource time) : PacketHandlerBase<SceneStateMsg, ISceneHandlerContext>
{
	private readonly ILogger<SceneStateHandler> _log = log;
	private readonly WorldEntryFanout _worldEntryFanout = worldEntryFanout;
	private readonly ITimeSource _time = time;

	protected override void Handle(ulong sender, SceneStateMsg msg, ISceneHandlerContext ctx)
	{
		var session = ctx.Session;
		// The reporter is the id the payload carries — and that id is a claim. On a
		// guest the claim is legitimate: the host relays ANOTHER member's change, so
		// the payload names that member while the transport sender is the host, and a
		// guest must not judge it. On the HOST an inbound frame is a member's own
		// report (SessionService.SendSceneState stamps the reporter's own id, the
		// local id never enters the presence table, and the host never relays a
		// member's change back to itself), so a claim naming somebody else is dropped
		// before it can move that member's scene state or earn it the whole entry
		// fan-out. The old fallback (`msg.SteamId != 0 ? msg.SteamId : sender`) took
		// the payload's word for whose state this is.
		var reporter = msg.SteamId;
		if (session.Role == SessionRole.Host && reporter != sender)
		{
			_log.LogWarning("Scene state dropped: transport={Transport} claimed={Claimed}.", sender, reporter);
			return;
		}

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
					// A fresh entry: the repair cadence of the PREVIOUS entry must not
					// suppress this one's first repair (EntryRepairSchedule).
					member.EntryRepair.Arm();
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
					// owned by WorldEntryFanout.
					_worldEntryFanout.Send(reporter);
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
		else if (session.Role == SessionRole.Host && member.InWorld && member.Handshaken)
		{
			// A REPEAT absolute report while the member is already in the world: the guest's
			// readiness window re-asserts its scene state while it is missing the host's two
			// control facts (SessionControlConvergence). A repeat therefore says two things at
			// once — the entry group sent on the edge did not complete this member, and the
			// member's uplink is up NOW — which is exactly the lazy-P2P swallow case the 60 s
			// repair cycle used to be the only heal for. Answer the first repeat (and a window
			// that stays open, at the member's repair cadence) with the entry state it may have
			// missed, BEFORE the two control facts, so the repair the marker completes is always
			// ahead of it. The marker's own meaning is narrower than "everything the entry
			// needs": the fan-out's entry-only members (the item snapshot and the radiation
			// line) stay entry contracts and are not part of this repair. The entry fan-out
			// itself is never re-run either — the repair set is the group built for this heal.
			if (member.EntryRepair.TryClaim(_time.NowMs))
			{
				_worldEntryFanout.SendInSessionRepair(reporter); // the runtime-owned absolute in-world tables
				session.FireEntryRepairRequested(reporter); // the adapter-owned entry tables (keypad codes, geyser types)
				_log.LogInformation("Repeat scene report from {Peer} — re-sent the entry state it may have missed (repair {Repairs} of this entry).",
					reporter, member.EntryRepair.Repairs);
			}

			ctx.World.AnswerRepeatInWorld(reporter);
			ctx.World.SendWorldSnapshotComplete(reporter);
			// Debug, not Information: a legitimately armed start gate (a slow loader, up to
			// the host's 30 s force-start) makes the window re-assert until the release, and
			// the window logs every re-report it sends — this line would only double it.
			_log.LogDebug("Repeat scene report from {Peer} — re-answered with the start-gate state and the entry-group marker.", reporter);
		}
	}
}
