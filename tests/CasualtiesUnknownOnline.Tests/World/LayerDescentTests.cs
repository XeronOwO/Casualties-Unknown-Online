using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The host's own layer advance as a shared edge (ticket
/// <c>layer-change-menu-round-trip</c>): the host announces it at the start of its
/// regeneration, every member in the world follows it once, and the start gate
/// treats those members as expected back even while their presence still reads
/// InWorld — that stale window is exactly what the old menu round-trip used to
/// make unnecessary.
/// <para>
/// What this suite proves is the wire decision and the gate's wait set. The member's
/// local generation itself is the game's own coroutine and is covered by the adapter
/// policy (<c>LayerDescentPolicyTests</c>) and the real-machine rows.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class LayerDescentTests
{
	private static List<bool> ListenForDescents(TestNode node)
	{
		var descents = new List<bool>();
		node.Services.GetRequiredService<IWorldControl>().LayerDescentReceived += twice => descents.Add(twice);
		return descents;
	}

	private static IWorldControl WorldOf(TestNode node) => node.Services.GetRequiredService<IWorldControl>();

	/// <summary>Both members are in the world the host is about to leave — the shape every descent starts from.</summary>
	private static void BothMembersInWorld(ItemSimWorld w)
	{
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(1f, 1f));
		w.G2.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(2f, 2f));
		w.Driver.Tick(500);
	}

	[Fact]
	public void TheHostsDescent_ReachesEveryMemberInTheWorldOnce()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 0);
		w.Driver.Tick(33); // the members follow the host's baseline before the descent
		BothMembersInWorld(w);
		var g1 = ListenForDescents(w.G1);
		var g2 = ListenForDescents(w.G2);
		var host = ListenForDescents(w.Host);

		WorldOf(w.Host).SendLayerDescent(twice: false);
		w.Driver.Tick(33);

		Assert.Equal([false], g1);
		Assert.Equal([false], g2);
		Assert.Empty(host);
	}

	[Fact]
	public void TheDrillPodsTwoLayerStep_RidesTheEdge()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 0);
		w.Driver.Tick(33);
		BothMembersInWorld(w);
		var g1 = ListenForDescents(w.G1);

		WorldOf(w.Host).SendLayerDescent(twice: true);
		w.Driver.Tick(33);

		Assert.Equal([true], g1);
	}

	[Fact]
	public void AMemberOutOfTheWorld_IsNotAskedToFollow()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 0);
		w.Driver.Tick(33);
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(1f, 1f));
		w.Driver.Tick(500);
		var g1 = ListenForDescents(w.G1);
		var g2 = ListenForDescents(w.G2);

		WorldOf(w.Host).SendLayerDescent(twice: false);
		w.Driver.Tick(33);

		Assert.Equal([false], g1);
		Assert.Empty(g2); // g2 is in the menu: the ordinary entry invites it, the descent does not pull it through
	}

	[Fact]
	public void ADescentForAnotherGeneration_IsRefused()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 2);
		w.Driver.Tick(33);
		var g1 = ListenForDescents(w.G1);

		// The edge names the layer the host has left; the member is in layer 2.
		w.Driver.Network.Deliver(w.Host.SteamId, w.G1.SteamId, NetPacket.Encode(NetMsg.LayerDescent,
			new LayerDescentMsg { Twice = false, Generation = WorldGenerationReports.StampOf(w.Host, layerOverride: 1) }));
		w.Driver.Tick(33);

		Assert.Empty(g1);
	}

	[Fact]
	public void ADescentWithNoGenerationStamp_IsRefused()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 0);
		w.Driver.Tick(33);
		var g1 = ListenForDescents(w.G1);

		w.Driver.Network.Deliver(w.Host.SteamId, w.G1.SteamId, NetPacket.Encode(NetMsg.LayerDescent, new LayerDescentMsg()));
		w.Driver.Tick(33);

		Assert.Empty(g1);
	}

	[Fact]
	public void TheAcceptedDescent_DropsTheBaselineThisMemberHolds()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 0);
		w.Driver.Tick(33);
		BothMembersInWorld(w);
		var guestWorld = WorldOf(w.G1);
		Assert.NotNull(guestWorld.WorldParams); // the member holds the run baseline it is playing in

		WorldOf(w.Host).SendLayerDescent(twice: false);
		w.Driver.Tick(33);

		// The layer being left is not the one to generate from: the boundary must hold until the host's
		// layer-advance baseline arrives.
		Assert.Null(guestWorld.WorldParams);
	}

	[Fact]
	public void TheStartGate_WaitsForTheMembersTheDescentWasSentTo_EvenWhilePresenceStillSaysInWorld()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 0);
		w.Driver.Tick(33);
		BothMembersInWorld(w);

		var hostWorld = WorldOf(w.Host);
		var readyOnGuest = 0;
		WorldOf(w.G1).WorldReadyReceived += () => readyOnGuest++;

		hostWorld.SendLayerDescent(twice: false);
		w.Driver.Tick(33);

		Assert.True(hostWorld.StartStartGate(), "the descent's members are expected back even though their presence still says InWorld");

		w.G1.Session.ReportSceneState(SceneStateType.InMenu, "SampleScene");
		w.G2.Session.ReportSceneState(SceneStateType.InMenu, "SampleScene");
		w.Driver.Tick(33);
		Assert.True(hostWorld.StartGateActive, "nobody is in the new world yet");

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(3f, 3f));
		w.Driver.Tick(500);
		Assert.True(hostWorld.StartGateActive, "g2 is still generating its own new layer");
		Assert.Equal(0, readyOnGuest);

		w.G2.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(4f, 4f));
		w.Driver.Tick(500);

		Assert.False(hostWorld.StartGateActive);
		Assert.Equal(1, readyOnGuest);
	}

	[Fact]
	public void AMemberThatFinishedBeforeTheHost_IsNotWaitedOn()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 0);
		w.Driver.Tick(33);
		BothMembersInWorld(w);

		var hostWorld = WorldOf(w.Host);
		hostWorld.SendLayerDescent(twice: false);
		w.Driver.Tick(33);

		// g1's own generation settled before the host entered its new world: the member reports the exit
		// and the new entry, which takes it out of the descent's expectation set.
		w.G1.Session.ReportSceneState(SceneStateType.InMenu, "SampleScene");
		w.Driver.Tick(33);
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(3f, 3f));
		w.Driver.Tick(500);

		Assert.True(hostWorld.StartStartGate(), "g2 is still generating");
		Assert.True(hostWorld.StartGateActive, "g1 already reported its new world, so only g2 is waited on");

		w.G2.Session.ReportSceneState(SceneStateType.InMenu, "SampleScene");
		w.Driver.Tick(33);
		w.G2.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(4f, 4f));
		w.Driver.Tick(500);

		Assert.False(hostWorld.StartGateActive);
	}

	[Fact]
	public void APriorRunsRelease_DoesNotPassAMemberThatSettledBeforeTheDescentGateArms()
	{
		// The run-start gate has released (the host is in the world, `_released` is true), and the
		// descent's edge arms its expectation. A member whose own new layer settles BEFORE the host's
		// new-world entry must still be held: neither the previous run's verdict nor the late-joiner
		// pass (the host may still read LocalInWorld) is this descent's release.
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 0);
		w.Driver.Tick(33);
		var hostWorld = WorldOf(w.Host);
		var readyOnGuest = 0;
		WorldOf(w.G1).WorldReadyReceived += () => readyOnGuest++;

		w.Host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		Assert.True(hostWorld.StartStartGate(), "the run-start gate arms");
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(1f, 1f));
		w.G2.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(2f, 2f));
		w.Driver.Tick(500);
		Assert.Equal(1, readyOnGuest);

		hostWorld.SendLayerDescent(twice: false);
		w.Driver.Tick(33);
		w.G1.Session.ReportSceneState(SceneStateType.InMenu, "SampleScene");
		w.Driver.Tick(33);
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(3f, 3f));
		w.Driver.Tick(500);

		Assert.Equal(1, readyOnGuest); // the descent's early arrival was NOT passed

		Assert.True(hostWorld.StartStartGate(), "the descent's gate waits for g2");
		Assert.True(hostWorld.StartGateActive, "g1 settled first but only g2 is still expected");

		w.G2.Session.ReportSceneState(SceneStateType.InMenu, "SampleScene");
		w.Driver.Tick(33);
		w.G2.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(4f, 4f));
		w.Driver.Tick(500);

		Assert.False(hostWorld.StartGateActive);
		Assert.Equal(2, readyOnGuest); // the descent's release reached g1
	}
}
