using static CasualtiesUnknownOnline.Tests.Mods.ModPacketsWorld;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The declared-packet declaration and send rails over the real three-node
/// star: the permission, the id grammar, the chain and count caps, the payload
/// rail, the session and role checks, and the channel's own guards (the wire
/// boundary answers for itself, the way the tunnel form's tests drive it).
/// Every refusal is a false return and a log line, and none of them sends a
/// frame.
/// </summary>
[Trait("Category", "Integration")]
public class ModPacketsRailsTests
{
	private const ulong G1Id = ModPacketsWorld.G1Id;
	private const ulong G2Id = ModPacketsWorld.G2Id;

	[Fact]
	public void PacketIds_AreListedInRegistrationOrder()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.Equal(
			[
				TestPacketMod.HostOnlyPacket,
				TestPacketMod.ReportPacket,
				TestPacketMod.AllPacket,
				TestPacketMod.BroadcastPacket,
				TestPacketMod.OrderedPacket,
				TestPacketMod.RefusingPacket,
				TestPacketMod.ThrowingPacket,
				TestPacketMod.SelfRetiringPacket,
				TestPacketMod.SelfSendPacket,
				TestPacketMod.RoundTripPacket,
			],
			ModPacketsWorld.Surface(w.Host).PacketIds);
		Assert.True(ModPacketsWorld.Surface(w.Host).CanSend);
	}

	[Fact]
	public void ADuplicateDeclaration_IsRefused()
	{
		var w = ModPacketsWorld.CreateThreeNode();
		var surface = ModPacketsWorld.Surface(w.Host);

		Assert.False(surface.Register(new ModPacket(
			TestPacketMod.ReportPacket,
			ModPacketSender.AnyMember,
			ModPacketDelivery.HostOnly,
			new ModPacketHandler(ModPacketStage.Apply, _ => { }))));
	}

	[Fact]
	public void ADeclarationWithoutAHandler_IsRefused()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.False(ModPacketsWorld.Surface(w.Host).Register(new ModPacket(
			"packets.empty",
			ModPacketSender.AnyMember,
			ModPacketDelivery.HostOnly)));
	}

	[Fact]
	public void ADeclarationWithAnInvalidId_IsRefused()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.False(ModPacketsWorld.Surface(w.Host).Register(new ModPacket(
			"Packets.Bad Id",
			ModPacketSender.AnyMember,
			ModPacketDelivery.HostOnly,
			new ModPacketHandler(ModPacketStage.Apply, _ => { }))));
	}

	[Fact]
	public void TheDeclarationCap_IsEnforced()
	{
		var w = ModPacketsWorld.CreateThreeNode();
		var surface = ModPacketsWorld.Surface(w.Host);
		var added = 0;

		while (surface.Register(new ModPacket(
			$"packets.fill{added}",
			ModPacketSender.AnyMember,
			ModPacketDelivery.HostOnly,
			new ModPacketHandler(ModPacketStage.Apply, _ => { }))))
		{
			added++;
			Assert.True(added <= ModPacketPolicy.MaxPacketsPerMod, "the cap must refuse before it is exceeded");
		}

		Assert.Equal(ModPacketPolicy.MaxPacketsPerMod, surface.PacketIds.Count);
	}

	[Fact]
	public void UndeclaredSend_IsRefused()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.False(ModPacketsWorld.Surface(w.G1).SendToHost("packets.missing", Step(1)));

		Assert.Empty(ModPacketsWorld.Fixture(w.Host).Runs);
	}

	[Fact]
	public void ANullOrOverCapValue_IsRefused()
	{
		var w = ModPacketsWorld.CreateThreeNode();

		Assert.False(ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.ReportPacket, null!));
		Assert.False(ModPacketsWorld.Surface(w.G1).SendToHost(TestPacketMod.ReportPacket, OverCapValue()));

		Assert.Empty(ModPacketsWorld.Fixture(w.Host).Runs);
	}

	[Fact]
	public void ASendOutsideASession_IsRefused()
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var steam = new FakeSteamService(G1Id);
		var g1 = TestNode.Create(G1Id, network, steam, clock, pumpFirstFrame: true);

		Assert.False(ModPacketsWorld.Surface(g1).SendToHost(TestPacketMod.ReportPacket, Step(1)));

		Assert.Empty(ModPacketsWorld.Fixture(g1).Runs);
	}

	[Fact]
	public void AModWithoutSendNetworkMessage_CannotDeclareOrSend()
	{
		var w = ModPacketsWorld.CreateThreeNode();
		var permissionless = (TestPermissionlessCommandMod)w.Host.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestPermissionlessCommandMod);

		Assert.False(permissionless.Context!.Packets.CanSend);
		Assert.False(permissionless.Context.Packets.Register(new ModPacket(
			"packets.noperm",
			ModPacketSender.AnyMember,
			ModPacketDelivery.HostOnly,
			new ModPacketHandler(ModPacketStage.Apply, _ => { }))));
		Assert.False(permissionless.Context.Packets.SendToHost("packets.noperm", Step(1)));
	}

	[Fact]
	public void AModThatDeclaresNoPackets_CannotSendOneItDoesNotOwn()
	{
		var w = ModPacketsWorld.CreateThreeNode();
		var echo = (TestEchoMod)w.G1.Services.GetRequiredService<ModService>().LoadedMods.Single(m => m is TestEchoMod);

		Assert.False(echo.Context!.Packets.SendToHost(TestPacketMod.ReportPacket, Step(1)));

		Assert.Empty(ModPacketsWorld.Fixture(w.Host).Runs);
	}

	[Fact]
	public void TheChannelItselfRefusesAPacketSendItCannotMake()
	{
		var w = ModPacketsWorld.CreateThreeNode();
		var hostChannel = w.Host.Services.GetRequiredService<ModChannel>();
		var guestChannel = w.G1.Services.GetRequiredService<ModChannel>();

		// The wire boundary answers for itself: the wrong role, the relay from a
		// guest, and an over-cap frame are all refused there too, so a caller
		// that bypassed the adapter cannot put an illegal frame on the wire.
		Assert.False(hostChannel.SendPacketToHost("test.packets", TestPacketMod.ReportPacket, [1]));
		Assert.False(guestChannel.SendPacketToAll("test.packets", TestPacketMod.ReportPacket, [1]));
		Assert.False(guestChannel.RelayPacket("test.packets", G2Id, TestPacketMod.ReportPacket, [1]));
		Assert.False(guestChannel.SendPacketToHost("test.packets", TestPacketMod.ReportPacket, new byte[ModChannel.MaxPayloadBytes + 1]));

		Assert.Empty(ModPacketsWorld.Fixture(w.Host).Runs);
		Assert.Empty(ModPacketsWorld.Fixture(w.G2).Runs);
	}
}
