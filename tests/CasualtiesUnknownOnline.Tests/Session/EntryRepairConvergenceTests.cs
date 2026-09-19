using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The world-entry repair on a repeat scene report (sync-coverage audit rows R3/W7; the cadence
/// review <c>review/sync-cadence-review.md</c>, finding 4).
///
/// <para>
/// The entry group is one-shot over a reliable transport whose retry only covers a lost frame
/// while the peer is reachable: a group sent before the lazy P2P session is up is dropped with
/// the sender none the wiser, and the next absolute send used to be the host's 60 s repair
/// cycle. The heal under test rides the guest's readiness window instead of a blind timer —
/// <see cref="SessionControlConvergence"/> re-asserts the scene report every 5 s while the
/// entry-group marker or the start-gate release is missing, so a REPEAT is proof that the entry
/// answer did not complete the member and that its uplink is up now. The host answers that
/// repeat with the absolute in-session repair set (and the adapter-owned entry tables, through
/// <see cref="ISessionControl.EntryRepairRequested"/>) BEFORE the two control facts, at the
/// <see cref="EntryRepairSchedule"/> cadence: free for an entry whose window closed on both
/// control facts, bounded for one a still-armed start gate holds open (the host cannot tell that
/// window from a swallowed one).
/// </para>
///
/// <para>
/// Every scenario arms the start gate and lets both guests load, which is what a real run does
/// (<c>WorldStartGate.Arm</c>): without a released gate the guest's window legitimately stays
/// open for its whole 60 s budget, and a scenario built on that would prove the wrong thing.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class EntryRepairConvergenceTests
{
	private const string Scene = "SampleScene";

	[Fact]
	public void SwallowedEntryGroup_IsHealedByTheRepeatAnswer()
	{
		using var w = ItemSimWorld.Create();
		RecordLayout(w);
		HostEnters(w);
		Assert.True(w.Host.Services.GetRequiredService<IWorldControl>().StartStartGate(), "the gate arms — it waits for both guests' InWorld reports");

		var frames = new List<NetMsg>();
		var answerBytes = 0L;
		w.G1.Transport.MessageReceived += (_, frame) =>
		{
			frames.Add((NetMsg)frame[0]);
			answerBytes += frame.Length;
		};

		// The adapter half's trigger: the runtime event the Game Adapter re-fans-out its entry
		// tables on (the adapter's own sends sit behind the live Unity world and cannot run in
		// this suite, so the event is the half it can observe).
		var requested = new List<ulong>();
		((ISessionControl)w.Host.Session).EntryRepairRequested += requested.Add;

		// The lazy P2P session is not up yet: the host's whole entry group is dropped while the
		// member's own report still arrives. The guest's window is armed locally either way.
		w.Driver.Network.SetFaults(w.Host.SteamId, w.G1.SteamId, new LinkFaults { Down = true });
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(5f, 6f));
		w.G2.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(7f, 8f));
		w.Driver.Tick(1_000);

		Assert.True(Member(w.Host, w.G1.SteamId).InWorld, "the member's report arrived — the host ran the entry group for it");
		Assert.Empty(frames);

		// The session comes up. The member is still re-asserting (neither control fact reached
		// it), so the next repeat — one window step later — is answered with the entry state it
		// missed; the wait is bounded at two steps.
		w.Driver.Network.ClearFaults(w.Host.SteamId, w.G1.SteamId);
		w.Driver.TickUntil(() => frames.Contains(NetMsg.WorldSnapshotComplete), maxMs: 10_000);

		Assert.True(frames.Contains(NetMsg.TrapLayoutSnapshot),
			"the answer to a repeat must carry the entry state the member missed, not only the marker");
		Assert.True(frames.IndexOf(NetMsg.TrapLayoutSnapshot) < frames.IndexOf(NetMsg.WorldSnapshotComplete),
			"the state must precede the completion marker, or the window would close on a marker whose tables never came");
		Assert.True(requested.Count == 1 && requested[0] == w.G1.SteamId,
			$"the repair must raise ISessionControl.EntryRepairRequested for the re-asserting member exactly once, got {requested.Count}");
		Assert.Equal(2_438L, answerBytes); // the pass size docs/evidence/sync-cadence-measurements.md records — this assertion is what makes that number reproducible
	}

	[Fact]
	public void WindowClosedByBothControlFacts_PaysNoRepair()
	{
		// Both guests load, so the gate releases and the marker arrives: the guest's window
		// closes on both facts and never re-asserts — the repair is free for that entry (the
		// held-gate case below is the one that DOES pay, which is why the claim is scoped to a
		// window closed by both facts rather than to a "clean entry").
		using var w = ItemSimWorld.Create();
		RecordLayout(w);
		HostEnters(w);
		Assert.True(w.Host.Services.GetRequiredService<IWorldControl>().StartStartGate(), "the gate arms");

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(5f, 6f));
		w.G2.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(7f, 8f));
		w.Driver.TickUntil(() => Member(w.Host, w.G1.SteamId).InWorld, maxMs: 6_000);
		w.Driver.Tick(1_000);

		Assert.Equal(1, w.ReceivedCount(w.G1, NetMsg.WorldSnapshotComplete));
		var layouts = w.ReceivedCount(w.G1, NetMsg.TrapLayoutSnapshot);

		for (var i = 0; i < 900; i++)
		{
			w.Driver.Tick(33); // 30 s — three repair intervals had the window stayed open
		}

		Assert.Equal(layouts, w.ReceivedCount(w.G1, NetMsg.TrapLayoutSnapshot));
		Assert.Equal(1, w.ReceivedCount(w.G1, NetMsg.WorldSnapshotComplete));
	}

	[Fact]
	public void HeldGate_PaysBoundedRepairsWithoutASwallow()
	{
		// A repeat does not say WHICH control fact is missing, so a window a legitimately armed
		// start gate holds open (its force-start is 30 s) receives the repair even though the
		// member lost nothing. This pins that cost as a measured bound instead of leaving it to
		// prose: 1 entry send + the claims at 5/15/25/35 s inside ~36 s.
		using var w = ItemSimWorld.Create();
		RecordLayout(w);
		HostEnters(w);
		Assert.True(w.Host.Services.GetRequiredService<IWorldControl>().StartStartGate(), "the gate arms — it waits for both guests' InWorld reports");

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(5f, 6f)); // g2 never loads
		for (var i = 0; i < 1_100; i++)
		{
			w.Driver.Tick(33); // ~36 s with the gate still armed
		}

		var sends = w.ReceivedCount(w.G1, NetMsg.TrapLayoutSnapshot);
		Assert.True(sends >= 2, "a window the gate holds open is answered, not ignored — the host cannot tell it from a swallowed one");
		Assert.True(sends <= 5, $"the held-gate window must stay bounded (1 entry send + 4 repairs inside 36 s), got {sends}");
	}

	[Fact]
	public void StillOpenWindow_IsAnsweredAtTheRepairCadence_AndThenStops()
	{
		using var w = ItemSimWorld.Create();
		RecordLayout(w);
		HostEnters(w);
		Assert.True(w.Host.Services.GetRequiredService<IWorldControl>().StartStartGate(), "the gate arms");

		// Only the marker is swallowed for g1: the entry group landed, so the host cannot tell
		// this window apart from a swallowed one — and the guest's window stays open for its
		// whole budget because the fact it waits for never arrives.
		Swallow(w, w.Host.SteamId, w.G1.SteamId, NetMsg.WorldSnapshotComplete);
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(5f, 6f));
		w.G2.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(7f, 8f));

		w.Driver.TickUntil(() => w.ReceivedCount(w.G1, NetMsg.TrapLayoutSnapshot) >= 2, maxMs: 30_000);
		Assert.True(w.ReceivedCount(w.G1, NetMsg.TrapLayoutSnapshot) >= 2,
			"a window that stays open must be answered again, not only once");

		for (var i = 0; i < 2_100; i++)
		{
			w.Driver.Tick(33); // ~70 s — the guest's window spends its budget inside this
		}

		var repairs = w.ReceivedCount(w.G1, NetMsg.TrapLayoutSnapshot);
		var bound = 1 + SessionControlConvergence.IntervalMs * SessionControlConvergence.MaxReports / EntryRepairSchedule.RepairIntervalMs;
		Assert.True(repairs <= bound,
			$"the repairs must be bounded by the guest's window ({bound} sends of this table), got {repairs}");

		for (var i = 0; i < 900; i++)
		{
			w.Driver.Tick(33); // 30 s more — a spent window must not trickle
		}

		Assert.Equal(repairs, w.ReceivedCount(w.G1, NetMsg.TrapLayoutSnapshot));
	}

	[Fact]
	public void ThirdMember_ReceivesNothingFromAnotherMembersEntryRepair()
	{
		using var w = ItemSimWorld.Create();
		RecordLayout(w);
		HostEnters(w);
		Assert.True(w.Host.Services.GetRequiredService<IWorldControl>().StartStartGate(), "the gate arms");

		Swallow(w, w.Host.SteamId, w.G1.SteamId, NetMsg.WorldSnapshotComplete); // only g1's marker is lost
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(5f, 6f));
		w.G2.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(7f, 8f));
		w.Driver.TickUntil(() => w.ReceivedCount(w.G1, NetMsg.TrapLayoutSnapshot) >= 2, maxMs: 30_000);

		var g2Layouts = w.ReceivedCount(w.G2, NetMsg.TrapLayoutSnapshot);
		var g2Markers = w.ReceivedCount(w.G2, NetMsg.WorldSnapshotComplete);

		for (var i = 0; i < 900; i++)
		{
			w.Driver.Tick(33); // 30 s of g1 repairs
		}

		Assert.Equal(g2Layouts, w.ReceivedCount(w.G2, NetMsg.TrapLayoutSnapshot)); // the runtime repair is per member
		Assert.Equal(g2Markers, w.ReceivedCount(w.G2, NetMsg.WorldSnapshotComplete));
	}

	/// <summary>The layer's trap scan: an entry-group member with no later edge, so it is the observable a repair must carry.</summary>
	private static void RecordLayout(ItemSimWorld w) =>
		w.Host.Services.GetRequiredService<IWorldControl>().ReportTrapLayout(EntityEventKind.SpikeStabbed, -13f, 466.8f, "spikestabber");

	private static void HostEnters(ItemSimWorld w) => w.Host.Session.ReportSceneState(SceneStateType.InWorld, Scene);

	/// <summary>Swallow one message type on one link — the lazy-P2P session drops the frame and reports the send as successful.</summary>
	private static void Swallow(ItemSimWorld w, ulong from, ulong to, NetMsg msg) =>
		w.Driver.Network.SetFaults(from, to, new LinkFaults { DropMessageId = msg });

	private static MemberPresenceTable.MemberPresence Member(TestNode node, ulong steamId) =>
		node.Session.Members.Single(m => m.SteamId == steamId);
}
