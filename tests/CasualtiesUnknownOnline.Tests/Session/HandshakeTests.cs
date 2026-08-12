using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The three-leg handshake over the fake network: lobby created (host
/// authority) → guest handshake → host ack → guest ack-ack — the exact code
/// path two processes take, with the lazy-session message swallow it retries.
/// </summary>
public class HandshakeTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	internal static (FakeNetwork Network, TestNode Host, TestNode Guest) CreateHostAndGuest()
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var guestSteam = new FakeSteamService(GuestId) { LobbyOwner = HostId, LobbyMembers = [HostId, GuestId] };
		// pumpFirstFrame: the mod discovery scan must run before any handshake
		// (a handshake arriving before it is refused as "mod check pending").
		var host = TestNode.Create(HostId, network, hostSteam, clock, pumpFirstFrame: true);
		var guest = TestNode.Create(GuestId, network, guestSteam, clock, pumpFirstFrame: true);
		return (network, host, guest);
	}

	[Fact]
	public void HostLobby_EstablishesAuthorityBeforeAnyGuest()
	{
		var (_, host, _) = CreateHostAndGuest();

		host.Steam.FireLobbyCreated(LobbyId);

		Assert.Equal(SessionRole.Host, host.Session.Role);
		Assert.Equal(HostId, host.Session.HostSteamId);
		Assert.True(host.Session.SessionActive);
	}

	[Fact]
	public void Handshake_CompletesEndToEnd()
	{
		var (_, host, guest) = CreateHostAndGuest();
		var hostActivations = 0;
		host.Session.SessionActivated += () => hostActivations++;
		var guestActivations = 0;
		guest.Session.SessionActivated += () => guestActivations++;

		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId]; // the guest joined the lobby
		guest.Steam.FireLobbyEntered(LobbyId);

		var hostMember = host.Session.Members.Single(m => m.SteamId == GuestId);
		Assert.True(hostMember.Handshaken, "host must confirm the guest end-to-end (ack-ack)");
		var guestMember = guest.Session.Members.Single(m => m.SteamId == HostId);
		Assert.True(guestMember.Handshaken, "guest must count the host as handshaken on the ack");
		// SessionActivated fires once on the side that completes the handshake —
		// the guest. The host activated at lobby creation (SessionActive) and has
		// no handshake of its own, so its event never fires (by design).
		Assert.Equal(0, hostActivations);
		Assert.Equal(1, guestActivations);
	}

	[Fact]
	public void Handshake_FirstMessageSwallowed_RetriesAfterInterval()
	{
		var (network, host, guest) = CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];

		network.Unregister(HostId); // the lazy P2P session swallows the first handshake
		guest.Steam.FireLobbyEntered(LobbyId);
		Assert.Empty(host.Session.Members);

		network.Register(host.Transport); // the P2P session establishes
		guest.Clock.Advance(1100); // past the 1 s handshake retry interval (virtual time — same clock as the network and the host)
		guest.Update(); // RetryHandshakeIfNeeded re-sends

		Assert.True(host.Session.Members.Single(m => m.SteamId == GuestId).Handshaken);
	}

	[Fact]
	public void RepeatedAckAck_DoesNotRefireMemberAdded()
	{
		// A retried handshake cycle re-delivers the AckAck — the member is
		// already Handshaken and MemberAdded must fire only on the
		// handshake→confirmed EDGE (every readiness subscriber — the item
		// domain's watermark grant, the Mod API's PlayerJoined — would
		// otherwise run again per retry). Found live in the two-process
		// verification: the join produced three PlayerJoined lines.
		var (network, host, guest) = CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		var memberAdded = 0;
		((ISessionControl)host.Session).MemberAdded += _ => memberAdded++;
		guest.Steam.FireLobbyEntered(LobbyId);
		Assert.Equal(1, memberAdded);

		// The guest's retry cycle re-acks (reconnect-style duplicate).
		var sender = guest.Services.GetRequiredService<PacketSender>();
		sender.Send(HostId, NetMsg.HandshakeAckAck, new HandshakeAckAckMsg());
		sender.Send(HostId, NetMsg.HandshakeAckAck, new HandshakeAckAckMsg());

		Assert.Equal(1, memberAdded);
	}
}
