using static CasualtiesUnknownOnline.Tests.Mods.ModPacketsWorld;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The declared-packet mechanism's failure story over the real three-node star:
/// an undeclared packet id, a frame from a peer that never completed a
/// handshake (both message forms), a packet id that is not a legal id at all, a
/// handler that throws, a Validate refusal, a chain that retires its own
/// declaration and a chain that starts its own packet. None of them may wedge
/// the receive path, and each is observable in the run history or the log.
/// </summary>
[Trait("Category", "Integration")]
public class ModPacketsFailureTests
{
	private static readonly string[] TwoStep = ModPacketsWorld.TwoStep;
	private static readonly string[] ThreeStep = ModPacketsWorld.ThreeStep;

	private const ulong G1Id = ModPacketsWorld.G1Id;
	private const ulong G2Id = ModPacketsWorld.G2Id;

	[Fact]
	public void ARefusingValidateStage_StopsTheChainAndTheRelay()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.RefusingPacket, Step(0)));

		Assert.Equal(["Validate:validate"], ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.RefusingPacket));
		Assert.Empty(ModPacketsWorld.Fixture(w.G2).RunsOf(TestPacketMod.RefusingPacket));
	}

	[Fact]
	public void ARelayedPacket_StillReachesTheOtherMemberWhenValidateAccepts()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.RefusingPacket, Step(1));

		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.RefusingPacket));
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.G2).RunsOf(TestPacketMod.RefusingPacket));
	}

	[Fact]
	public void AThrowingHandler_IsIsolatedAndTheRestOfTheChainStillRuns()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.ThrowingPacket, Step(0));

		Assert.Equal(ThreeStep, ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.ThrowingPacket));
	}

	[Fact]
	public void AThrowingHandler_DoesNotWedgeTheNextPacket()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.ThrowingPacket, Step(0));
		Assert.True(ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.ReportPacket, Step(1)));

		Assert.Equal(ThreeStep, ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.ReportPacket));
	}

	[Fact]
	public void AnUndeclaredPacketId_IsDroppedAtTheReceivingCopy()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		w.Host.Transport.Deliver(G1Id, ModPacketsWorld.Frame("packets.missing", Step(1)));

		Assert.Empty(ModPacketsWorld.Fixture(w.Host).Runs);
	}

	[Fact]
	public void APacketIdThatIsNotALegalId_IsDroppedByItsLengthAlone()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		// A peer chooses this text: it is refused by the registration grammar
		// before anything echoes it, and the log names its length instead.
		w.Host.Transport.Deliver(G1Id, ModPacketsWorld.Frame(new string('x', 4096), Step(1)));

		Assert.Empty(ModPacketsWorld.Fixture(w.Host).Runs);
	}

	[Fact]
	public void AFrameFromAPeerThatIsNoMemberAtAll_ReachesNoChain()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		w.Host.Transport.Deliver(4242, ModPacketsWorld.Frame(TestPacketMod.ReportPacket, Step(1)));

		Assert.Empty(ModPacketsWorld.Fixture(w.Host).Runs);
	}

	[Fact]
	public void AFrameFromAMemberThatHasNotHandshaken_ReachesNoChain()
	{
		var w = ModPacketsWorld.CreateThreeNode();
		var peer = G1Id + 777;

		// A presence row WITHOUT the handshake bit — the shape the gate exists
		// for, and the one a "not a member at all" frame cannot exercise.
		((ISessionControl)w.Host.Session).GetOrCreateMember(peer);
		w.Host.Transport.Deliver(peer, ModPacketsWorld.Frame(TestPacketMod.ReportPacket, Step(1)));

		Assert.Empty(ModPacketsWorld.Fixture(w.Host).Runs);
	}

	[Fact]
	public void AFrameFromAMemberThatHasNotHandshaken_ReachesNoTunnelCallbackEither()
	{
		var w = ModPacketsWorld.CreateThreeNode();
		var peer = G1Id + 778;
		var echo = (TestEchoMod)w.Host.Services.GetRequiredService<ModService>().LoadedMods.Single(m => m is TestEchoMod);

		((ISessionControl)w.Host.Session).GetOrCreateMember(peer);
		w.Host.Transport.Deliver(peer, NetPacket.Encode(NetMsg.ModMessage, new ModMessageMsg
		{
			ModId = "test.echo",
			Payload = [1],
		}));

		Assert.Empty(echo.Received); // the gate covers the anonymous tunnel form too
	}

	[Fact]
	public void AChainThatRetiresItsOwnDeclaration_StillFinishes()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.Host).SendToHost(TestPacketMod.SelfRetiringPacket, Step(1)));

		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.SelfRetiringPacket));
		Assert.False(ModPacketsWorld.Surface(w.Host).IsRegistered(TestPacketMod.SelfRetiringPacket));
		Assert.False(ModPacketsWorld.Surface(w.Host).SendToHost(TestPacketMod.SelfRetiringPacket, Step(1)));
	}

	[Fact]
	public void AChainThatSendsItsOwnPacket_IsRefusedInsteadOfRecursing()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.True(ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.SelfSendPacket, Step(1)));

		// Exactly one run per copy: the nested send is refused, while the outer
		// delivery still completes its own send and relay.
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.G1).RunsOf(TestPacketMod.SelfSendPacket));
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.Host).RunsOf(TestPacketMod.SelfSendPacket));
		Assert.Equal(TwoStep, ModPacketsWorld.Fixture(w.G2).RunsOf(TestPacketMod.SelfSendPacket));
	}
}
