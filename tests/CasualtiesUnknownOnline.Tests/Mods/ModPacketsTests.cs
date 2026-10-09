using static CasualtiesUnknownOnline.Tests.Mods.ModPacketsWorld;
using CasualtiesUnknownOnline.Abstractions;
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

		Assert.True(ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.ReportPacket, Step(1)));

		Assert.Equal(ThreeStep, ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.ReportPacket));
		Assert.Equal(ThreeStep, ModPacketsWorld.Fixture(w.G2).RunsOf(TestPacketMod.ReportPacket));
		Assert.Empty(ModPacketsWorld.Fixture(w.G1).RunsOf(TestPacketMod.ReportPacket)); // the declaration excludes the sender
	}

	[Fact]
	public void GuestReport_ReachesTheHostWithTheReportersIdentity_AndTheRelayCarriesTheHosts()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.ReportPacket, Step(1));

		Assert.Equal(G1Id, ModPacketsWorld.Fixture(w.Host).Runs[0].Sender);
		Assert.Equal(HostId, ModPacketsWorld.Fixture(w.G2).Runs[0].Sender); // the other members hear it from the host
	}

	[Fact]
	public void GuestReport_EveryMember_RunsOnTheReporterFirst()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.AllPacket, Step(1)));

		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.G1).RunsOf(TestPacketMod.AllPacket));
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.AllPacket));
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.G2).RunsOf(TestPacketMod.AllPacket));
		Assert.Equal(G1Id, ModPacketsWorld.Fixture(w.G1).Runs[0].Sender); // the reporter's own run is local
	}

	[Fact]
	public void HostBroadcast_EveryMember_RunsOnEveryCopyIncludingTheHostsOwn()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.Host).Broadcast(TestPacketMod.AllPacket, Step(2)));

		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.AllPacket));
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.G1).RunsOf(TestPacketMod.AllPacket));
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.G2).RunsOf(TestPacketMod.AllPacket));
		Assert.Equal(HostId, ModPacketsWorld.Fixture(w.Host).Runs[0].Sender);
	}

	[Fact]
	public void HostBroadcast_EveryOtherMember_SkipsTheHostsOwnCopy()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.Host).Broadcast(TestPacketMod.BroadcastPacket, Step(2)));

		Assert.Empty(ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.BroadcastPacket));
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.G1).RunsOf(TestPacketMod.BroadcastPacket));
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.G2).RunsOf(TestPacketMod.BroadcastPacket));
	}

	[Fact]
	public void HostDirectedSend_ReachesOnlyThatMember_AndNotTheHostsOwnCopy()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.Host).SendToPeer(G2Id, TestPacketMod.BroadcastPacket, Step(3)));

		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.G2).RunsOf(TestPacketMod.BroadcastPacket));
		Assert.Empty(ModPacketsWorld.Fixture(w.G1).RunsOf(TestPacketMod.BroadcastPacket));
		Assert.Empty(ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.BroadcastPacket));
	}

	[Fact]
	public void HostLocalSend_HostOnlyPacket_RunsOnTheHostAndNowhereElse()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.Host).SendToHost(TestPacketMod.HostOnlyPacket, Step(1)));

		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.HostOnlyPacket));
		Assert.Empty(ModPacketsWorld.Fixture(w.G1).Runs);
		Assert.Empty(ModPacketsWorld.Fixture(w.G2).Runs);
	}

	[Fact]
	public void HostLocalSend_EveryMember_RunsOnTheHostsOwnCopyOnly()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.Host).SendToHost(TestPacketMod.AllPacket, Step(1)));

		// The host reaches its own copy without a wire hop, and nothing else runs.
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.AllPacket));
		Assert.Empty(ModPacketsWorld.Fixture(w.G1).Runs);
		Assert.Empty(ModPacketsWorld.Fixture(w.G2).Runs);
	}

	[Fact]
	public void HostDirectedSend_EveryMember_RunsOnTheHostAndThatMember()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.Host).SendToPeer(G2Id, TestPacketMod.AllPacket, Step(3)));

		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.AllPacket));
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.G2).RunsOf(TestPacketMod.AllPacket));
		Assert.Empty(ModPacketsWorld.Fixture(w.G1).RunsOf(TestPacketMod.AllPacket));
	}

	// ---- Who may send ----

	[Fact]
	public void HostOnlyPacket_AGuestCannotStartIt()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.False(ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.HostOnlyPacket, Step(1)));

		Assert.Empty(ModPacketsWorld.Fixture(w.Host).Runs);
	}

	[Fact]
	public void HostOnlyPacket_AMembersFrameForItIsRefusedAtTheHost()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		w.Host.Transport.Deliver(G1Id, ModPacketsWorld.Frame(TestPacketMod.HostOnlyPacket, Step(1)));

		Assert.Empty(ModPacketsWorld.Fixture(w.Host).Runs);
	}

	[Fact]
	public void GuestOnlyPacket_TheHostCannotStartIt()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.False(ModPacketsWorld.Surface(w.Host).Broadcast(TestPacketMod.ReportPacket, Step(1)));

		Assert.Empty(ModPacketsWorld.Fixture(w.Host).Runs);
		Assert.Empty(ModPacketsWorld.Fixture(w.G1).Runs);
	}

	[Fact]
	public void AGuestCannotBroadcastOrSendToAMember()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.False(ModPacketsWorld.Surface(w.G1).Broadcast(TestPacketMod.AllPacket, Step(1)));
		Assert.False(ModPacketsWorld.Surface(w.G1).SendToPeer(G2Id, TestPacketMod.AllPacket, Step(1)));

		Assert.Empty(ModPacketsWorld.Fixture(w.G1).Runs);
		Assert.Empty(ModPacketsWorld.Fixture(w.G2).Runs);
	}

	[Fact]
	public void HostOnlyDelivery_DirectedSendAndBroadcastAreRefused()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.False(ModPacketsWorld.Surface(w.Host).SendToPeer(G2Id, TestPacketMod.HostOnlyPacket, Step(1)));
		Assert.False(ModPacketsWorld.Surface(w.Host).Broadcast(TestPacketMod.HostOnlyPacket, Step(1)));

		Assert.Empty(ModPacketsWorld.Fixture(w.G2).Runs);
	}

	[Fact]
	public void EveryOtherMember_TheHostHasNoOtherCopyToSendTo()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.False(ModPacketsWorld.Surface(w.Host).SendToHost(TestPacketMod.BroadcastPacket, Step(1)));

		Assert.Empty(ModPacketsWorld.Fixture(w.Host).Runs);
	}

	[Fact]
	public void DirectedSend_ToANonMember_IsRefused()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.False(ModPacketsWorld.Surface(w.Host).SendToPeer(G1Id + 999, TestPacketMod.BroadcastPacket, Step(1)));

		Assert.Empty(ModPacketsWorld.Fixture(w.G1).Runs);
		Assert.Empty(ModPacketsWorld.Fixture(w.G2).Runs);
	}

	// ---- The chain and its payload ----

	[Fact]
	public void TheDeclaredChain_RunsStagesInFrameworkOrder_AndKeepsTheDeclaredOrderInsideOne()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.Host).SendToHost(TestPacketMod.OrderedPacket, Step(1)));

		Assert.Equal(
			["Validate:v1", "Apply:a1", "Apply:a2", "Observe:o1"],
			ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.OrderedPacket));
	}

	[Fact]
	public void OneImmutableValue_ServesTheWholeDelivery()
	{
		var w = ModPacketsWorld.CreateThreeNode();
		var value = ModValue.Map(("step", ModValue.Integer(1)), ("tags", ModValue.List(ModValue.Text("a"), ModValue.Text("b"))));

		// EveryMember: the reporter's own copy runs locally, the host runs its
		// copy, the other member runs on the relayed frame. A value is immutable,
		// so every copy reads exactly what the sender handed in — the model's own
		// encoding round-trips a nested value — and the two handlers of ONE
		// delivery read the same value, with no per-delivery copy in between.
		Assert.True(ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.RoundTripPacket, value));

		Assert.Equal([value, value], ModPacketsWorld.ValuesOf(w.G1, TestPacketMod.RoundTripPacket));
		Assert.Equal([value, value], ModPacketsWorld.ValuesOf(w.Host, TestPacketMod.RoundTripPacket));
		Assert.Equal([value, value], ModPacketsWorld.ValuesOf(w.G2, TestPacketMod.RoundTripPacket));
	}

	[Fact]
	public void ALocalRefusal_StopsTheFrameBeforeItLeaves()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		// EveryMember runs the reporter's own copy first, and its validate stage
		// refuses a step below 1 — so nothing leaves the reporter at all.
		Assert.False(ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.AllPacket, Step(0)));

		Assert.Equal(["Validate:validate"], ModPacketsWorld.Fixture(w.G1).RunsOf(TestPacketMod.AllPacket));
		Assert.Empty(ModPacketsWorld.Fixture(w.Host).Runs);
		Assert.Empty(ModPacketsWorld.Fixture(w.G2).Runs);
	}

	[Fact]
	public void PacketFloodOverBurst_IsDroppedLikeTheTunnelForm()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		for (var i = 0; i < ModRateLimitPolicy.ModMessageBurst + 1; i++)
		{
			ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.ReportPacket, Step(1));
		}

		Assert.Equal(
			ModRateLimitPolicy.ModMessageBurst,
			ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.ReportPacket).Count / ThreeStep.Length);
	}
}
