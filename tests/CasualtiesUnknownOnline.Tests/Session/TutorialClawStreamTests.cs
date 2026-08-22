using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Tutorial;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The tutorial-claw presentation stream: the host publishes the claw state,
/// the service broadcasts at the configured state-stream cadence to in-world
/// guests, and the guest applies new snapshots through the seq gate. Locked
/// through the real handler over the fake network (same pattern as
/// <c>EnemySyncServiceTests</c>).
/// </summary>
public class TutorialClawStreamTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static TutorialClawStateMsg State(uint seq = 0, float x = 1f, float y = 2f) =>
		new()
		{
			Seq = seq,
			HandPosX = x,
			HandPosY = y,
			HandPosCurrentX = x + 1f,
			HandPosCurrentY = y + 1f,
			GrabKind = TutorialClawStateMsg.GrabNone,
			Material = TutorialClawStateMsg.MaterialOpen,
		};

	[Fact]
	public void WireRoundTrip_PreservesClawState()
	{
		var msg = State(seq: 7, x: 11f, y: 22f);
		msg.GrabKind = TutorialClawStateMsg.GrabItem;
		msg.Material = TutorialClawStateMsg.MaterialClosed;
		msg.ArmKnifeSpriteOverride = true;

		var frame = NetPacket.Encode(NetMsg.TutorialClawState, msg);
		var decoded = NetPacket.DecodePayload<TutorialClawStateMsg>(frame);

		Assert.Equal(7u, decoded.Seq);
		Assert.Equal(11f, decoded.HandPosX);
		Assert.Equal(22f, decoded.HandPosY);
		Assert.Equal(TutorialClawStateMsg.GrabItem, decoded.GrabKind);
		Assert.Equal(TutorialClawStateMsg.MaterialClosed, decoded.Material);
		Assert.True(decoded.ArmKnifeSpriteOverride);
	}

	[Fact]
	public void HostPublish_ReachesInWorldGuest_AtStateStreamCadence()
	{
		using var w = ItemSimWorld.Create();
		var hostClaw = w.Host.Services.GetRequiredService<TutorialClawService>();
		var guestClaw = w.G1.Services.GetRequiredService<TutorialClawService>();

		var received = 0;
		guestClaw.TutorialClawStateReceived += _ => received++;

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		hostClaw.PublishTutorialClawState(State(x: 10f, y: 20f));

		// Past the 50 ms default throttle; the 20 Hz broadcast reaches the guest.
		w.Driver.Tick(60);
		Assert.True(received > 0, "the host's published claw state must reach an in-world guest");
	}

	[Fact]
	public void NonInWorldGuest_DoesNotReceiveStream()
	{
		using var w = ItemSimWorld.Create();
		var hostClaw = w.Host.Services.GetRequiredService<TutorialClawService>();
		var guestClaw = w.G1.Services.GetRequiredService<TutorialClawService>();

		var received = 0;
		guestClaw.TutorialClawStateReceived += _ => received++;

		hostClaw.PublishTutorialClawState(State(x: 10f, y: 20f));
		w.Driver.Tick(120);

		Assert.Equal(0, received);
	}

	[Fact]
	public void StaleAndDuplicateSequences_Dropped_NewerPass()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using var hostDispose = host;
		using var guestDispose = guest;
		var control = guest.Services.GetRequiredService<ITutorialClawControl>();
		var sender = host.Services.GetRequiredService<PacketSender>();

		var received = 0;
		control.TutorialClawStateReceived += _ => received++;

		sender.Send(GuestId, NetMsg.TutorialClawState, State(seq: 1), reliable: false);
		Assert.Equal(1, received);

		sender.Send(GuestId, NetMsg.TutorialClawState, State(seq: 1), reliable: false); // duplicate
		sender.Send(GuestId, NetMsg.TutorialClawState, State(seq: 0), reliable: false); // stale
		Assert.Equal(1, received);

		sender.Send(GuestId, NetMsg.TutorialClawState, State(seq: 2), reliable: false);
		Assert.Equal(2, received);
	}

	[Fact]
	public void ClearStopsFurtherBroadcasts()
	{
		using var w = ItemSimWorld.Create();
		var hostClaw = w.Host.Services.GetRequiredService<TutorialClawService>();
		var guestClaw = w.G1.Services.GetRequiredService<TutorialClawService>();

		var received = 0;
		guestClaw.TutorialClawStateReceived += _ => received++;

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		hostClaw.PublishTutorialClawState(State(x: 10f, y: 20f));
		w.Driver.Tick(60);
		Assert.True(received > 0);

		received = 0;
		hostClaw.ClearTutorialClawState();
		w.Driver.Tick(120);
		Assert.Equal(0, received);
	}
}
