using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The declared-packet mechanism's routing and policy half over the real
/// three-node star: who may start a packet, which copies run its chain, the
/// relay the framework owes a declaration, the stage order, the payload-copy
/// contract and the shared rate limit. Every delivery is asserted by what
/// actually ran on each copy, so "the reporter's copy did not run" is a fact of
/// the run history and not an inference.
///
/// The failure story lives in <see cref="ModPacketsFailureTests"/> and the
/// declaration/send rails in <see cref="ModPacketsRailsTests"/>; the three
/// classes share <see cref="ModPacketsWorld"/>.
/// </summary>
[Trait("Category", "Integration")]
public class ModPacketsTests
{
	private static readonly string[] TwoStep = ModPacketsWorld.TwoStep;
	private static readonly string[] ThreeStep = ModPacketsWorld.ThreeStep;

	private const ulong HostId = ModPacketsWorld.HostId;
	private const ulong G1Id = ModPacketsWorld.G1Id;
	private const ulong G2Id = ModPacketsWorld.G2Id;

	// ---- Who runs the chain ----

	[Fact]
	public void GuestReport_EveryOtherMember_RunsOnTheHostAndTheOtherMemberOnly()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.ReportPacket, [1]));

		Assert.Equal(ThreeStep, ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.ReportPacket));
		Assert.Equal(ThreeStep, ModPacketsWorld.Fixture(w.G2).RunsOf(TestPacketMod.ReportPacket));
		Assert.Empty(ModPacketsWorld.Fixture(w.G1).RunsOf(TestPacketMod.ReportPacket)); // the declaration excludes the sender
	}

	[Fact]
	public void GuestReport_ReachesTheHostWithTheReportersIdentity_AndTheRelayCarriesTheHosts()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.ReportPacket, [1]);

		Assert.Equal(G1Id, ModPacketsWorld.Fixture(w.Host).Runs[0].Sender);
		Assert.Equal(HostId, ModPacketsWorld.Fixture(w.G2).Runs[0].Sender); // the other members hear it from the host
	}

	[Fact]
	public void GuestReport_EveryMember_RunsOnTheReporterFirst()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.AllPacket, [1]));

		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.G1).RunsOf(TestPacketMod.AllPacket));
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.AllPacket));
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.G2).RunsOf(TestPacketMod.AllPacket));
		Assert.Equal(G1Id, ModPacketsWorld.Fixture(w.G1).Runs[0].Sender); // the reporter's own run is local
	}

	[Fact]
	public void HostBroadcast_EveryMember_RunsOnEveryCopyIncludingTheHostsOwn()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.Host).Broadcast(TestPacketMod.AllPacket, [2]));

		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.AllPacket));
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.G1).RunsOf(TestPacketMod.AllPacket));
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.G2).RunsOf(TestPacketMod.AllPacket));
		Assert.Equal(HostId, ModPacketsWorld.Fixture(w.Host).Runs[0].Sender);
	}

	[Fact]
	public void HostBroadcast_EveryOtherMember_SkipsTheHostsOwnCopy()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.Host).Broadcast(TestPacketMod.BroadcastPacket, [2]));

		Assert.Empty(ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.BroadcastPacket));
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.G1).RunsOf(TestPacketMod.BroadcastPacket));
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.G2).RunsOf(TestPacketMod.BroadcastPacket));
	}

	[Fact]
	public void HostDirectedSend_ReachesOnlyThatMember_AndNotTheHostsOwnCopy()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.Host).SendToPeer(G2Id, TestPacketMod.BroadcastPacket, [3]));

		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.G2).RunsOf(TestPacketMod.BroadcastPacket));
		Assert.Empty(ModPacketsWorld.Fixture(w.G1).RunsOf(TestPacketMod.BroadcastPacket));
		Assert.Empty(ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.BroadcastPacket));
	}

	[Fact]
	public void HostLocalSend_HostOnlyPacket_RunsOnTheHostAndNowhereElse()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.Host).SendToHost(TestPacketMod.HostOnlyPacket, [1]));

		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.HostOnlyPacket));
		Assert.Empty(ModPacketsWorld.Fixture(w.G1).Runs);
		Assert.Empty(ModPacketsWorld.Fixture(w.G2).Runs);
	}

	[Fact]
	public void HostLocalSend_EveryMember_RunsOnTheHostsOwnCopyOnly()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.Host).SendToHost(TestPacketMod.AllPacket, [1]));

		// The host reaches its own copy without a wire hop, and nothing else runs.
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.AllPacket));
		Assert.Empty(ModPacketsWorld.Fixture(w.G1).Runs);
		Assert.Empty(ModPacketsWorld.Fixture(w.G2).Runs);
	}

	[Fact]
	public void HostDirectedSend_EveryMember_RunsOnTheHostAndThatMember()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.Host).SendToPeer(G2Id, TestPacketMod.AllPacket, [3]));

		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.AllPacket));
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.G2).RunsOf(TestPacketMod.AllPacket));
		Assert.Empty(ModPacketsWorld.Fixture(w.G1).RunsOf(TestPacketMod.AllPacket));
	}

	// ---- Who may send ----

	[Fact]
	public void HostOnlyPacket_AGuestCannotStartIt()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.False(ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.HostOnlyPacket, [1]));

		Assert.Empty(ModPacketsWorld.Fixture(w.Host).Runs);
	}

	[Fact]
	public void HostOnlyPacket_AMembersFrameForItIsRefusedAtTheHost()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		w.Host.Transport.Deliver(G1Id, ModPacketsWorld.Frame(TestPacketMod.HostOnlyPacket, [1]));

		Assert.Empty(ModPacketsWorld.Fixture(w.Host).Runs);
	}

	[Fact]
	public void GuestOnlyPacket_TheHostCannotStartIt()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.False(ModPacketsWorld.Surface(w.Host).Broadcast(TestPacketMod.ReportPacket, [1]));

		Assert.Empty(ModPacketsWorld.Fixture(w.Host).Runs);
		Assert.Empty(ModPacketsWorld.Fixture(w.G1).Runs);
	}

	[Fact]
	public void AGuestCannotBroadcastOrSendToAMember()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.False(ModPacketsWorld.Surface(w.G1).Broadcast(TestPacketMod.AllPacket, [1]));
		Assert.False(ModPacketsWorld.Surface(w.G1).SendToPeer(G2Id, TestPacketMod.AllPacket, [1]));

		Assert.Empty(ModPacketsWorld.Fixture(w.G1).Runs);
		Assert.Empty(ModPacketsWorld.Fixture(w.G2).Runs);
	}

	[Fact]
	public void HostOnlyDelivery_DirectedSendAndBroadcastAreRefused()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.False(ModPacketsWorld.Surface(w.Host).SendToPeer(G2Id, TestPacketMod.HostOnlyPacket, [1]));
		Assert.False(ModPacketsWorld.Surface(w.Host).Broadcast(TestPacketMod.HostOnlyPacket, [1]));

		Assert.Empty(ModPacketsWorld.Fixture(w.G2).Runs);
	}

	[Fact]
	public void EveryOtherMember_TheHostHasNoOtherCopyToSendTo()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.False(ModPacketsWorld.Surface(w.Host).SendToHost(TestPacketMod.BroadcastPacket, [1]));

		Assert.Empty(ModPacketsWorld.Fixture(w.Host).Runs);
	}

	[Fact]
	public void DirectedSend_ToANonMember_IsRefused()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.False(ModPacketsWorld.Surface(w.Host).SendToPeer(G1Id + 999, TestPacketMod.BroadcastPacket, [1]));

		Assert.Empty(ModPacketsWorld.Fixture(w.G1).Runs);
		Assert.Empty(ModPacketsWorld.Fixture(w.G2).Runs);
	}

	// ---- The chain and its payload ----

	[Fact]
	public void TheDeclaredChain_RunsStagesInFrameworkOrder_AndKeepsTheDeclaredOrderInsideOne()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.Host).SendToHost(TestPacketMod.OrderedPacket, [1]));

		Assert.Equal(
			["Validate:v1", "Apply:a1", "Apply:a2", "Observe:o1"],
			ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.OrderedPacket));
	}

	[Fact]
	public void AHandlersInPlaceWrite_StaysInsideItsOwnDelivery()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		// EveryMember: the reporter's own copy runs locally, the host runs its
		// copy, the other member runs on the relayed frame. A validate handler
		// rewrites the payload in place, so the FIRST byte each copy saw proves
		// what that copy received, and the second row proves the rewrite reached
		// the delivery's own later handler.
		Assert.True(ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.RewritePacket, [1]));

		Assert.Equal([(byte)1, (byte)99], ModPacketsWorld.BytesOf(w.G1, TestPacketMod.RewritePacket));
		Assert.Equal([(byte)1, (byte)99], ModPacketsWorld.BytesOf(w.Host, TestPacketMod.RewritePacket));
		Assert.Equal([(byte)1, (byte)99], ModPacketsWorld.BytesOf(w.G2, TestPacketMod.RewritePacket));
	}

	[Fact]
	public void ALocalRefusal_StopsTheFrameBeforeItLeaves()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		// EveryMember runs the reporter's own copy first, and its validate stage
		// refuses a zero first byte — so nothing leaves the reporter at all.
		Assert.False(ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.RewritePacket, [0]));

		Assert.Equal(["Validate:validate"], ModPacketsWorld.Fixture(w.G1).RunsOf(TestPacketMod.RewritePacket));
		Assert.Empty(ModPacketsWorld.Fixture(w.Host).Runs);
		Assert.Empty(ModPacketsWorld.Fixture(w.G2).Runs);
	}

	[Fact]
	public void PacketFloodOverBurst_IsDroppedLikeTheTunnelForm()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		for (var i = 0; i < ModRateLimitPolicy.ModMessageBurst + 1; i++)
		{
			ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.ReportPacket, [1]);
		}

		Assert.Equal(
			ModRateLimitPolicy.ModMessageBurst,
			ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.ReportPacket).Count / ThreeStep.Length);
	}
}
