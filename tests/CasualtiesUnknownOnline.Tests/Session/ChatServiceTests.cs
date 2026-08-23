using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Chat;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The text-chat flow over the fake network: a guest's report reaches the host
/// buffer and is relayed to the other guests, the host's own line reaches every
/// guest, invalid/whitespace/oversized lines are refused locally, and a spoofed
/// sender is dropped at the host. This is the same star relay the production
/// plugin uses, with no manual acceptance.
/// </summary>
public class ChatServiceTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong OtherGuestId = 3001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void GuestChat_ReachesHostBuffer()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostChat = host.Services.GetRequiredService<IChatControl>();
		var guestChat = guest.Services.GetRequiredService<IChatControl>();

		Assert.True(guestChat.TrySend("hello from guest"));
		var driver = new SimulationDriver(guest.Clock, guest.Transport.Network, host, guest);
		driver.TickUntil(() => hostChat.Recent.Any(l => l.SenderSteamId == GuestId), maxMs: 1000);

		var line = hostChat.Recent.Single(l => l.SenderSteamId == GuestId);
		Assert.Equal("hello from guest", line.Text);
		Assert.Contains(guestChat.Recent, l => l.SenderSteamId == GuestId && l.Text == "hello from guest");
	}

	[Fact]
	public void HostChat_ReachesGuestBuffer()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostChat = host.Services.GetRequiredService<IChatControl>();
		var guestChat = guest.Services.GetRequiredService<IChatControl>();

		Assert.True(hostChat.TrySend("host says hi"));
		var driver = new SimulationDriver(guest.Clock, guest.Transport.Network, host, guest);
		driver.TickUntil(() => guestChat.Recent.Any(l => l.SenderSteamId == HostId), maxMs: 1000);

		var line = guestChat.Recent.Single(l => l.SenderSteamId == HostId);
		Assert.Equal("host says hi", line.Text);
		Assert.Contains(hostChat.Recent, l => l.SenderSteamId == HostId && l.Text == "host says hi");
	}

	[Fact]
	public void GuestChat_IsRelayedToTheOtherGuest()
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var firstGuestSteam = new FakeSteamService(GuestId) { LobbyOwner = HostId, LobbyMembers = [HostId, GuestId] };
		var secondGuestSteam = new FakeSteamService(OtherGuestId) { LobbyOwner = HostId, LobbyMembers = [HostId, GuestId, OtherGuestId] };

		var host = TestNode.Create(HostId, network, hostSteam, clock, pumpFirstFrame: true);
		var guest = TestNode.Create(GuestId, network, firstGuestSteam, clock, pumpFirstFrame: true);
		var otherGuest = TestNode.Create(OtherGuestId, network, secondGuestSteam, clock, pumpFirstFrame: true);

		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId, OtherGuestId];
		guest.Steam.FireLobbyEntered(LobbyId);
		otherGuest.Steam.FireLobbyEntered(LobbyId);

		var hostChat = host.Services.GetRequiredService<IChatControl>();
		var guestChat = guest.Services.GetRequiredService<IChatControl>();
		var otherChat = otherGuest.Services.GetRequiredService<IChatControl>();

		Assert.True(guestChat.TrySend("relay me"));
		var driver = new SimulationDriver(clock, network, host, guest, otherGuest);
		driver.TickUntil(() =>
			hostChat.Recent.Any(l => l.Text == "relay me")
			&& otherChat.Recent.Any(l => l.Text == "relay me"), maxMs: 2000);

		Assert.Contains(hostChat.Recent, l => l.SenderSteamId == GuestId && l.Text == "relay me");
		Assert.Contains(otherChat.Recent, l => l.SenderSteamId == GuestId && l.Text == "relay me");
		Assert.DoesNotContain(otherChat.Recent, l => l.SenderSteamId == OtherGuestId && l.Text == "relay me");
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("x")]
	public void InvalidOrOversizedLine_IsRefusedLocally(string text)
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var guestChat = guest.Services.GetRequiredService<IChatControl>();
		var hostChat = host.Services.GetRequiredService<IChatControl>();

		var textToTry = text == "x" ? new string('x', 201) : text;
		Assert.False(guestChat.TrySend(textToTry));
		var driver = new SimulationDriver(guest.Clock, guest.Transport.Network, host, guest);
		// Give any accidental send a chance to arrive; the host must remain silent.
		driver.Tick(500);

		Assert.Empty(hostChat.Recent);
		Assert.Empty(guestChat.Recent);
	}

	[Fact]
	public void SpoofedSender_IsDroppedAtHost()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostChat = host.Services.GetRequiredService<IChatControl>();
		var sender = guest.Services.GetRequiredService<PacketSender>();

		sender.Send(HostId, NetMsg.Chat, new ChatMsg
		{
			SenderSteamId = OtherGuestId, // transport sender is GuestId, but claims another author
			Text = "spoofed",
		});

		var driver = new SimulationDriver(guest.Clock, guest.Transport.Network, host, guest);
		driver.Tick(500);

		Assert.Empty(hostChat.Recent);
	}

	[Fact]
	public void SessionEnd_ClearsChatBuffer()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var guestChat = guest.Services.GetRequiredService<IChatControl>();
		Assert.True(guestChat.TrySend("hello"));
		Assert.NotEmpty(guestChat.Recent);

		guest.Session.EndSession();
		Assert.Empty(guestChat.Recent);
	}
}
