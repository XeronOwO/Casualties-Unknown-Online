using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Session-control convergence (sync-coverage audit rows R3/R4, plus the W7 inert
/// marker). The session control messages are one-shot over a reliable transport,
/// but the transport only retries while the peer is reachable: the documented
/// lazy-P2P swallow window drops frames the sender never learns about. Three of
/// those had no re-report at all — the handshake's third leg (a swallowed
/// <c>HandshakeAckAck</c> left the host's member unconfirmed for the whole
/// connection), the guest's <c>SceneState</c> InWorld report (the host never ran
/// the entry fan-out, never started the entity sync, and the guest's own 60 s
/// start-gate valve was the only escape), the start-gate release
/// (<c>WorldReady</c>), and the roster (<c>PlayerJoin</c>).
/// <para>
/// The fix under test: the host re-sends the ack it sent while the member stays
/// unconfirmed; the guest re-asserts its ABSOLUTE scene report in a bounded
/// window after each world entry and stops when the host's two control facts are
/// in (the world-entry completion marker and the start-gate release); the host
/// answers a repeat report with exactly those facts and never re-runs the entry
/// fan-out; and the roster rides the in-session repair group as an absolute
/// table, absorbed by identity. Every scenario below swallows exactly the frame
/// under test on a link that still reports success — the production swallow
/// contract.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class SessionControlConvergenceTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;
	private const string Scene = "SampleScene";

	/// <summary>Swallow one message type on one link — the lazy-P2P session drops the frame and reports the send as successful.</summary>
	private static void Swallow(ItemSimWorld w, ulong from, ulong to, NetMsg msg) =>
		w.Driver.Network.SetFaults(from, to, new LinkFaults { DropMessageId = msg });

	private static void Heal(ItemSimWorld w, ulong from, ulong to) => w.Driver.Network.ClearFaults(from, to);

	private static MemberPresenceTable.MemberPresence Member(TestNode node, ulong steamId) =>
		node.Session.Members.Single(m => m.SteamId == steamId);

	/// <summary>The host owns the run and enters the world first; the guest follows on its own report.</summary>
	private static void HostEnters(ItemSimWorld w) => w.Host.Session.ReportSceneState(SceneStateType.InWorld, Scene);

	/// <summary>A live count of one message type the HOST receives from one sender (the sim world records the guests' wire surfaces only).</summary>
	private sealed class HostWireCounter(TestNode host, NetMsg msg, ulong? from = null)
	{
		private int _count;

		internal void Bind() => host.Transport.MessageReceived += (sender, frame) =>
		{
			if ((NetMsg)frame[0] == msg && (from is null || sender == from))
			{
				_count++;
			}
		};

		internal int Count => _count;
	}

	[Fact]
	public void HandshakeAckAckDropped_TheHostReAcksUntilTheMemberIsConfirmed()
	{
		// R4: the guest stops retrying the handshake the moment the ack arrives, so a
		// swallowed third leg used to leave the host's member unconfirmed for the rest
		// of the connection — the start gate and the entity sync both exclude it.
		var (network, host, guest) = HandshakeTests.CreateHostAndGuest();
		network.SetFaults(GuestId, HostId, new LinkFaults { DropMessageId = NetMsg.HandshakeAckAck });

		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		Assert.True(guest.Session.SessionActive, "the guest got the ack, so it believes it is connected");
		Assert.True(guest.Session.Members.Single(m => m.SteamId == HostId).Handshaken);
		Assert.False(host.Session.Members.Single(m => m.SteamId == GuestId).Handshaken,
			"the swallowed third leg must leave the member unconfirmed");

		// The swallow window ends: the host's warm-up pump re-sends the ack, the guest
		// answers every ack with the third leg again, and the loop closes.
		network.ClearFaults(GuestId, HostId);
		for (var i = 0; i < 300 && !host.Session.Members.Single(m => m.SteamId == GuestId).Handshaken; i++)
		{
			network.Advance(33);
			host.Update();
			guest.Update();
		}

		Assert.True(host.Session.Members.Single(m => m.SteamId == GuestId).Handshaken,
			"the host must confirm the member after re-sending the ack (R4)");
	}

	[Fact]
	public void InWorldReportDropped_TheWindowHealsItAndTheEntryGroupFiresOnce()
	{
		// R3: a swallowed InWorld report used to leave the member out of the world for
		// the whole connection — no entry fan-out, no entity sync, and the guest's 60 s
		// valve back to the menu as the only escape.
		using var w = ItemSimWorld.Create();
		HostEnters(w);
		Swallow(w, w.G1.SteamId, w.Host.SteamId, NetMsg.SceneState);

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(5f, 6f));
		w.Driver.Tick(100);

		Assert.False(Member(w.Host, w.G1.SteamId).InWorld, "the swallowed report must not mark the member in world");

		Heal(w, w.G1.SteamId, w.Host.SteamId);
		w.Driver.TickUntil(() => Member(w.Host, w.G1.SteamId).InWorld, maxMs: 6_000);

		Assert.True(Member(w.Host, w.G1.SteamId).InWorld);
		Assert.Equal(1, w.ReceivedCount(w.G1, NetMsg.WorldSnapshotComplete)); // the entry group ran exactly once (the re-report is an edge, not a repeat)
	}

	[Fact]
	public void WorldReadyDropped_TheRepeatReportIsAnsweredWithTheGateState()
	{
		// R3: the start-gate release is a one-shot; a guest that missed it waits at the
		// gate until its own 60 s valve returns it to the menu.
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		var readyOnGuest = 0;
		guestWorld.WorldReadyReceived += () => readyOnGuest++;

		HostEnters(w);
		Assert.True(hostWorld.StartStartGate(), "the gate arms — it waits for both guests' InWorld reports");
		Swallow(w, w.Host.SteamId, w.G1.SteamId, NetMsg.WorldReady);

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(5f, 6f));
		w.G2.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(7f, 8f));
		w.Driver.Tick(500);

		Assert.False(hostWorld.StartGateActive, "the released gate must not stay armed");
		Assert.Equal(0, readyOnGuest); // the release was swallowed

		Heal(w, w.Host.SteamId, w.G1.SteamId);
		w.Driver.TickUntil(() => readyOnGuest > 0, maxMs: 8_000);
		Assert.Equal(1, readyOnGuest);

		// Both facts are in — the window is closed, so no further re-report and no
		// second release for the same entry.
		var releases = w.ReceivedCount(w.G1, NetMsg.WorldReady);
		for (var i = 0; i < 900; i++)
		{
			w.Driver.Tick(33); // 30 s of frames — six more windows had the window stayed open
		}

		Assert.Equal(releases, w.ReceivedCount(w.G1, NetMsg.WorldReady));
		Assert.Equal(1, readyOnGuest);
	}

	[Fact]
	public void RosterDropped_ConvergesThroughTheRepairGroupWithoutADuplicateJoin()
	{
		// R3: the roster announcement is one-shot too. A swallowed activation join left
		// the guest without its own entity (invisible, no state stream) until it left and
		// re-entered the world.
		using var w = ItemSimWorld.Create();
		var entities = w.G1.Services.GetRequiredService<EntitySyncService>();
		var joins = 0;
		entities.RemoteJoined += _ => joins++;

		HostEnters(w);
		Swallow(w, w.Host.SteamId, w.G1.SteamId, NetMsg.PlayerJoin);
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(5f, 6f));
		w.Driver.Tick(500);

		Assert.False(entities.EntitySyncActive, "the swallowed activation join must not start the guest's own entity");

		Heal(w, w.Host.SteamId, w.G1.SteamId);
		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(100);

		Assert.True(entities.EntitySyncActive, "the roster repair must activate the guest's own entity");
		Assert.Equal(1, joins);

		// A repeat repair carries the SAME identity and is absorbed — no second join.
		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(100);
		Assert.Equal(1, joins);
	}

	[Fact]
	public void LostMarker_KeepsTheWindowOpenAndClosesItWhenItArrives()
	{
		// W7/R3: the completion marker is the entry group's acknowledgement — this is its
		// production consumer. The gate release is in, only the marker is missing, so the
		// window stays open until the host's repeat answer carries it; once it lands, the
		// window closes and the guest stops re-asserting.
		using var w = ItemSimWorld.Create();
		var sceneReports = new HostWireCounter(w.Host, NetMsg.SceneState, w.G1.SteamId);
		sceneReports.Bind();

		HostEnters(w);
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		Assert.True(hostWorld.StartStartGate(), "the gate arms — it waits for both guests' InWorld reports");
		Swallow(w, w.Host.SteamId, w.G1.SteamId, NetMsg.WorldSnapshotComplete);

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(5f, 6f));
		w.G2.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(7f, 8f));
		w.Driver.TickUntil(() => sceneReports.Count >= 3, maxMs: 20_000);

		Assert.Equal(3, sceneReports.Count); // g1's live report + two re-reports: the gate release arrived, the marker did not

		Heal(w, w.Host.SteamId, w.G1.SteamId);
		w.Driver.TickUntil(() => w.ReceivedCount(w.G1, NetMsg.WorldSnapshotComplete) >= 1, maxMs: 10_000);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.WorldSnapshotComplete) >= 1, "the repeat answer must carry the marker");
		var settled = sceneReports.Count;
		for (var i = 0; i < 1_200; i++)
		{
			w.Driver.Tick(33); // 40 s of frames — room for eight more windows had the window stayed open
		}

		Assert.Equal(settled, sceneReports.Count); // the marker closed the window — no further re-report
	}

	[Fact]
	public void SpentWindow_StopsReReportingInsteadOfTrickling()
	{
		// The window is bounded: a member the host never answers is named in a warning
		// and the re-reports stop — the pattern the other fallbacks follow (a budget per
		// entry, never a permanent per-minute trickle).
		using var w = ItemSimWorld.Create();
		var sceneReports = new HostWireCounter(w.Host, NetMsg.SceneState, w.G1.SteamId);
		sceneReports.Bind();

		HostEnters(w);
		Swallow(w, w.G1.SteamId, w.Host.SteamId, NetMsg.SceneState); // the live report is swallowed
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(5f, 6f));
		w.Driver.Tick(1_000);

		Heal(w, w.G1.SteamId, w.Host.SteamId);
		Swallow(w, w.Host.SteamId, w.G1.SteamId, NetMsg.WorldSnapshotComplete); // the answers are swallowed from here on

		w.Driver.TickUntil(() => sceneReports.Count >= SessionControlConvergence.MaxReports, maxMs: 90_000);
		Assert.Equal(SessionControlConvergence.MaxReports, sceneReports.Count);

		for (var i = 0; i < 1_800; i++)
		{
			w.Driver.Tick(33); // 60 s of frames — six more windows had the budget not been spent
		}

		Assert.Equal(SessionControlConvergence.MaxReports, sceneReports.Count);
	}

	[Fact]
	public void ThirdMember_ReceivesNothingFromAnotherMembersConvergence()
	{
		// Row 6 of the matrix: a repeat report is answered to ITS member only. Both guests
		// are in the world and only g1 is missing the release, so g2's window closes while
		// g1's keeps being answered — the third party's surface must stand still.
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		HostEnters(w);
		Assert.True(hostWorld.StartStartGate(), "the gate arms — it waits for both guests' InWorld reports");
		Swallow(w, w.Host.SteamId, w.G1.SteamId, NetMsg.WorldReady); // only g1's release is lost
		Swallow(w, w.G1.SteamId, w.Host.SteamId, NetMsg.SceneState);  // …and its InWorld report with it

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(5f, 6f));
		w.G2.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(7f, 8f));
		w.Driver.Tick(500);
		Heal(w, w.G1.SteamId, w.Host.SteamId);
		w.Driver.TickUntil(() => Member(w.Host, w.G1.SteamId).InWorld, maxMs: 6_000);

		var g1Markers = w.ReceivedCount(w.G1, NetMsg.WorldSnapshotComplete);
		var g2Markers = w.ReceivedCount(w.G2, NetMsg.WorldSnapshotComplete);
		var g2Releases = w.ReceivedCount(w.G2, NetMsg.WorldReady);
		Assert.Equal(1, g1Markers);          // g1's entry group ran exactly once
		Assert.True(g2Markers >= 1, "g2 loaded normally and got its own entry group");
		Assert.True(g2Releases >= 1, "g2's release arrived with the gate release broadcast");

		for (var i = 0; i < 600; i++)
		{
			w.Driver.Tick(33); // 20 s — g1's window keeps re-asserting (its release never arrives)
		}

		Assert.True(w.ReceivedCount(w.G1, NetMsg.WorldSnapshotComplete) > g1Markers,
			"g1's repeats must keep being answered — otherwise this test proves nothing");
		Assert.Equal(g2Markers, w.ReceivedCount(w.G2, NetMsg.WorldSnapshotComplete)); // …and g2 saw none of it
		Assert.Equal(g2Releases, w.ReceivedCount(w.G2, NetMsg.WorldReady));
	}

	[Fact]
	public void GateArmedPastTheFirstWindows_StillConvergesWhenItReleases()
	{
		// The host's gate can legitimately stay armed for its whole 30 s force-start window,
		// and the release broadcast is itself a one-shot: a budget shorter than the gate's
		// lifetime would stop re-asserting before the message it heals could even be lost.
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		var readyOnGuest = 0;
		guestWorld.WorldReadyReceived += () => readyOnGuest++;

		HostEnters(w);
		Assert.True(hostWorld.StartStartGate(), "the gate arms — it waits for both guests' InWorld reports");
		Swallow(w, w.Host.SteamId, w.G1.SteamId, NetMsg.WorldReady);

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(5f, 6f));
		for (var i = 0; i < 1_100; i++)
		{
			// ~36 s with the gate still armed (g2 never loads): every re-report is answered
			// with nothing, which is the branch the window must survive rather than spend.
			w.Driver.Tick(33);
		}

		Assert.True(hostWorld.StartGateActive, "the gate must still be armed — the adapter's force-start is not pumped here");
		Assert.Equal(0, readyOnGuest);

		// g2 finally loads: the gate releases and g1's copy of the broadcast is swallowed too.
		w.G2.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(7f, 8f));
		w.Driver.Tick(100);
		Assert.False(hostWorld.StartGateActive, "the released gate must not stay armed");
		Assert.Equal(0, readyOnGuest);

		// The window is still open past its first windows, so the next re-report is answered
		// with the gate's verdict instead of the guest falling through to the 60 s valve.
		Heal(w, w.Host.SteamId, w.G1.SteamId);
		w.Driver.TickUntil(() => readyOnGuest > 0, maxMs: 10_000);
		Assert.Equal(1, readyOnGuest);
	}

	[Fact]
	public void ExitReportDropped_IsReAssertedInBoundedWindows()
	{
		// The reverse edge: a swallowed InMenu report used to leave the host holding the
		// member in world for the rest of the session (its entity stream and clones stay
		// alive). Nothing acknowledges an exit, so that direction is a bounded repeat —
		// it heals inside the swallow window and then stops instead of trickling.
		using var w = ItemSimWorld.Create();
		var sceneReports = new HostWireCounter(w.Host, NetMsg.SceneState, w.G1.SteamId);
		sceneReports.Bind();

		HostEnters(w);
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(5f, 6f));
		w.Driver.TickUntil(() => Member(w.Host, w.G1.SteamId).InWorld, maxMs: 6_000);

		Swallow(w, w.G1.SteamId, w.Host.SteamId, NetMsg.SceneState);
		w.G1.Session.ReportSceneState(SceneStateType.InMenu, Scene);
		w.Driver.Tick(500);
		Assert.True(Member(w.Host, w.G1.SteamId).InWorld, "the swallowed exit must leave the host holding the member in world");

		Heal(w, w.G1.SteamId, w.Host.SteamId);
		w.Driver.TickUntil(() => !Member(w.Host, w.G1.SteamId).InWorld, maxMs: 10_000);
		Assert.False(Member(w.Host, w.G1.SteamId).InWorld, "the exit re-assert must reach the host");

		for (var i = 0; i < 1_200; i++)
		{
			w.Driver.Tick(33); // 40 s — the exit window spends the rest of its budget here
		}

		// The live InWorld report plus exactly the exit window's budget: the bound is the policy.
		Assert.Equal(1 + SessionControlConvergence.MaxExitReports, sceneReports.Count);
		var spent = sceneReports.Count;
		for (var i = 0; i < 1_200; i++)
		{
			w.Driver.Tick(33); // …and then it stops instead of trickling
		}

		Assert.Equal(spent, sceneReports.Count);
	}
}
