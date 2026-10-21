using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The start gate's LATE-JOINER verdict (<see cref="WorldStartGate"/>).
///
/// <para>
/// The gate holds the world's start until every member that was loading when the
/// host entered is in, and its release is the one control fact a guest waits for
/// before it starts playing. A member that arrives AFTER the run is already
/// running passes directly, and the host answers a re-asserted InWorld report
/// with the gate's CURRENT verdict, so a swallowed release heals inside the
/// guest's readiness window instead of falling through to its 60 s valve back to
/// the menu.
/// </para>
///
/// <para>
/// A run can be PLAYING while no gate was ever armed for it: the host entered the
/// world with no session (solo), then created the lobby from inside that running
/// world. <see cref="WorldStartGate.Arm"/> is only called on the host's own
/// InWorld edge, so nothing marked that run's gate released, and the arriving
/// member's release was never answered (batch 20261021-e, session B: both members
/// were returned to the menu by their 60 s valve, and the host's repair cycle then
/// correctly found no in-world member). The fixtures below reproduce that gate
/// STATE — host in the world with no gate armed for this run — by simply not
/// arming the gate. They reproduce the gate's flags, not the production flow: the
/// fixture's session is already active when the host reports InWorld, while the
/// production host entered with no session at all (the adapter's host phase and
/// loading-screen presentation are outside this suite; the batch reads them).
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class WorldStartGateTests
{
	private const string Scene = "SampleScene";

	/// <summary>Swallow one message type on one link — the lazy-P2P session drops the frame and reports the send as successful.</summary>
	private static void Swallow(ItemSimWorld w, ulong from, ulong to, NetMsg msg) =>
		w.Driver.Network.SetFaults(from, to, new LinkFaults { DropMessageId = msg });

	private static void Heal(ItemSimWorld w, ulong from, ulong to) => w.Driver.Network.ClearFaults(from, to);

	/// <summary>The host's world entry happened while the run had no session — the gate never armed, so no release was ever broadcast.</summary>
	private static void HostEnteredBeforeAnySession(ItemSimWorld w) =>
		w.Host.Session.ReportSceneState(SceneStateType.InWorld, Scene);

	[Fact]
	public void RunAlreadyPlayingWhenTheMemberArrived_PassesWithoutAnArmedGate()
	{
		using var w = ItemSimWorld.Create();
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		var readyOnGuest = 0;
		guestWorld.WorldReadyReceived += () => readyOnGuest++;

		HostEnteredBeforeAnySession(w);
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(5f, 6f));
		w.Driver.Tick(500);

		Assert.Equal(1, readyOnGuest);
	}

	[Fact]
	public void SwallowedReleaseIntoAnAlreadyPlayingRun_IsAnsweredOnTheRepeat()
	{
		// The release into a never-armed gate is still a one-shot message over a
		// transport that can drop it: the repeat report must be answered with the
		// gate's verdict, exactly like a released gate's late joiner.
		using var w = ItemSimWorld.Create();
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		var readyOnGuest = 0;
		guestWorld.WorldReadyReceived += () => readyOnGuest++;

		HostEnteredBeforeAnySession(w);
		Swallow(w, w.Host.SteamId, w.G1.SteamId, NetMsg.WorldReady);
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(5f, 6f));
		w.Driver.Tick(500);

		Assert.Equal(0, readyOnGuest); // the release was swallowed

		Heal(w, w.Host.SteamId, w.G1.SteamId);
		w.Driver.TickUntil(() => readyOnGuest > 0, maxMs: 8_000);

		Assert.Equal(1, readyOnGuest);
	}

	[Fact]
	public void ANewRun_ForgetsThePreviousRunsRelease()
	{
		// A second run in the same session clicks start from the menu, and its gate only arms at the
		// host's own world entry. Until then the run has no verdict of its own: the previous run's
		// release must not pass a member whose world came up first.
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		var readyOnGuest = 0;
		guestWorld.WorldReadyReceived += () => readyOnGuest++;

		w.Host.Session.ReportSceneState(SceneStateType.InWorld, Scene);
		Assert.True(hostWorld.StartStartGate(), "the first run's gate arms");
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(5f, 6f));
		w.G2.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(7f, 8f));
		w.Driver.Tick(500);
		Assert.Equal(1, readyOnGuest); // the first run released

		w.Host.Session.ReportSceneState(SceneStateType.InMenu, "PreGen"); // the host leaves for the next run
		hostWorld.SetHostRunPending(true); // the click: the new run begins
		w.Driver.Tick(100);

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(5f, 6f)); // the member's world is up first
		w.Driver.Tick(500);

		Assert.Equal(1, readyOnGuest);
	}

	[Fact]
	public void AnArmedGate_StillHoldsTheMemberUntilItsOwnRelease()
	{
		// The never-armed branch must not turn into "release whenever the host is in
		// the world": a gate that IS armed over this run still holds its members
		// until every slot reports in.
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		var readyOnGuest = 0;
		guestWorld.WorldReadyReceived += () => readyOnGuest++;

		w.Host.Session.ReportSceneState(SceneStateType.InWorld, Scene);
		Assert.True(hostWorld.StartStartGate(), "the gate arms — it waits for both guests' InWorld reports");

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(5f, 6f));
		w.Driver.Tick(500);

		Assert.True(hostWorld.StartGateActive, "g2 is still loading — the gate must stay armed");
		Assert.Equal(0, readyOnGuest);

		w.G2.Session.ReportSceneState(SceneStateType.InWorld, Scene, new NetVector2(7f, 8f));
		w.Driver.Tick(500);

		Assert.False(hostWorld.StartGateActive);
		Assert.Equal(1, readyOnGuest);
	}
}
