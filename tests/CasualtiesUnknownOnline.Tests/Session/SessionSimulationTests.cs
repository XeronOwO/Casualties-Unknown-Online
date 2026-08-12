using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Phase-2 session simulations: time-driven scenarios over the full stack —
/// the handshake under link faults (the lazy P2P session swallow it retries),
/// the presence check's exact 2 s cadence, the disconnect→rejoin rebuild and
/// the RTT reading under an injected link delay (the shared virtual clock
/// makes the 600 ms round trip exact). Every scenario runs on the production
/// handlers over the fake network, with the virtual clock replacing wall time.
/// </summary>
public class SessionSimulationTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static bool Handshaken(TestNode host, TestNode guest) =>
		host.Session.Members.Any(m => m.SteamId == GuestId && m.Handshaken) &&
		guest.Session.Members.Any(m => m.SteamId == HostId && m.Handshaken);

	[Fact]
	public void Handshake_SurvivesLinkOutage_RecoversWhenLinkReturns()
	{
		var (network, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];

		// The lazy P2P session: no traffic in either direction yet.
		network.SetFaults(GuestId, HostId, new LinkFaults { Down = true });
		network.SetFaults(HostId, GuestId, new LinkFaults { Down = true });
		guest.Steam.FireLobbyEntered(LobbyId);
		Assert.Empty(host.Session.Members);

		// Several retry intervals pass while the link stays down — nothing gets through.
		for (var i = 0; i < 5; i++)
		{
			guest.Clock.Advance(1000);
			guest.Update();
		}

		Assert.Empty(host.Session.Members);
		Assert.True(guest.Session.Role == SessionRole.Guest, "the role survives the outage — the lobby identity");

		// The P2P session establishes — the next retry lands.
		network.ClearFaults(GuestId, HostId);
		network.ClearFaults(HostId, GuestId);
		var driver = new SimulationDriver(guest.Clock, network, host, guest);
		driver.TickUntil(() => Handshaken(host, guest), maxMs: 5000);

		Assert.True(Handshaken(host, guest), "the retry loop must complete the handshake end-to-end");
	}

	[Fact]
	public void PresenceCheck_RunsExactlyEveryTwoSeconds()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		host.Update(); // first pump: the presence check runs immediately and arms the next one at +2 s

		host.Steam.LobbyMembers = [HostId]; // the guest vanished from the lobby
		host.Clock.Advance(1900);
		host.Update();

		Assert.True(host.Session.SessionActive, "1.9 s after the change the check has not fired yet (interval is 2 s)");

		host.Clock.Advance(200);
		host.Update();

		Assert.False(host.Session.SessionActive, "2.1 s after the change the check fired and ended the session");
		Assert.Empty(host.Session.Members);
	}

	[Fact]
	public void Guest_HostDisconnectThenRejoin_RebuildsSession()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);

		// The host vanishes from the lobby — after the 2 s check the guest ends its session (no host migration).
		guest.Steam.LobbyMembers = [GuestId];
		guest.Clock.Advance(2100);
		guest.Update();
		Assert.False(guest.Session.SessionActive);
		Assert.True(guest.Session.Role == SessionRole.Guest, "the lobby identity survives — the rejoin path rebuilds on it");

		// The host returns: lobby membership restored, the guest re-enters (the game's
		// rejoin flow fires the lobby-entered callback again), the handshake rebuilds.
		guest.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);
		guest.Update();

		Assert.True(Handshaken(host, guest), "the rebuilt session must handshake end-to-end again");
	}

	[Fact]
	public void Rtt_ReflectsInjectedLinkDelayExactly()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		// 300 ms each way — the shared virtual clock makes the round trip exact.
		host.Transport.Network.SetFaults(HostId, GuestId, new LinkFaults { DelayMs = 300 });
		host.Transport.Network.SetFaults(GuestId, HostId, new LinkFaults { DelayMs = 300 });

		host.Session.RequestPing();
		// One hop per advance, each exactly one one-way latency: the ping reaches the
		// guest at +300 (its pong is scheduled for +600), the pong at +600 — the RTT
		// the requester computes from its own clock must read exactly 600.
		host.Transport.Network.Advance(300);
		host.Transport.Network.Advance(300);

		Assert.True(Math.Abs(host.Session.LastRttMs - 600f) < 1f,
			$"the RTT must read the injected round trip (600 ms), was {host.Session.LastRttMs} ms");
	}

	[Theory]
	[InlineData(1)]
	[InlineData(7)]
	[InlineData(42)]
	public void Handshake_UnderRandomLinkJitter_AlwaysConverges(int seed)
	{
		var (network, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var rng = new Random(seed);
		// Random fault windows while the handshake retries: ~30 % of each second the
		// link is down, otherwise a 50-800 ms delay; the final state is always clean.
		for (var second = 0; second < 8; second++)
		{
			var faults = rng.NextDouble() < 0.3
				? new LinkFaults { Down = true }
				: new LinkFaults { DelayMs = 50 + rng.Next(750) };
			network.SetFaults(GuestId, HostId, faults);
			network.SetFaults(HostId, GuestId, faults);
			guest.Clock.Advance(1000);
			guest.Update();
		}

		network.ClearFaults(GuestId, HostId);
		network.ClearFaults(HostId, GuestId);

		var driver = new SimulationDriver(guest.Clock, network, host, guest);
		driver.TickUntil(() => Handshaken(host, guest), maxMs: 5000);

		Assert.True(Handshaken(host, guest), $"seed {seed}: the handshake must converge once the link stabilises");
	}

	[Fact]
	public void Host_WarmsUpUnhandshakenLobbyPeers()
	{
		// 09ccc87: the lazy P2P session only establishes with traffic from BOTH
		// directions — a guest retrying the handshake alone never arrives. The
		// host pings lobby peers that have not completed a handshake (the
		// SendPeerWarmup pump, every retry interval).
		var (network, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId]; // the guest is IN the lobby but never entered/handshook

		var pings = 0;
		guest.Transport.MessageReceived += (_, frame) =>
		{
			if ((Runtime.Protocol.NetMsg)frame[0] == CasualtiesUnknownOnline.Runtime.Protocol.NetMsg.Ping)
			{
				pings++;
			}
		};

		var driver = new SimulationDriver(guest.Clock, network, host, guest);
		driver.Tick(3500); // more than one retry interval (3 s)

		Assert.True(pings > 0, "the host must warm up the un-handshaken lobby peer with pings");
	}

	[Fact]
	public void LocalSteamId_NonZero_AfterInitialize()
	{
		// 12b30a8: the SteamId was snapshotted in the constructor — before the
		// Steam init — and read 0 (the guest's input all vanished). The entity
		// SteamId is now taken after the Initialize phase; the TestNode drives
		// the same lifecycle, so the local id must be the real one.
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);

		Assert.True(host.Session.LocalSteamId == HostId, $"host's local id must be {HostId}, got {host.Session.LocalSteamId}");
		Assert.True(guest.Session.LocalSteamId == GuestId, $"guest's local id must be {GuestId}, got {guest.Session.LocalSteamId}");
		Assert.True(host.Session.LocalSteamId != 0 && guest.Session.LocalSteamId != 0, "the id must never be the pre-init 0");
	}
}
