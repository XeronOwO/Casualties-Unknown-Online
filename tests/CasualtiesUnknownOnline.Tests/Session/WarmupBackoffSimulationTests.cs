using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Full-stack simulations for the warm-up backoff: the host's
/// SendPeerWarmup pump reacts to the transport's send verdict — a broken
/// Steam P2P session no longer gets a ping every retry interval; a healed
/// link is retried at the backoff schedule and a success resets the peer to
/// the normal 1 s cadence.
/// </summary>
public class WarmupBackoffSimulationTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void Host_WarmupBacksOff_AfterTransportFailure_AndResetsOnSuccess()
	{
		var (network, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId]; // the guest is in the lobby but has not handshaken

		var deliveredAt = new List<long>();
		guest.Transport.MessageReceived += (_, frame) =>
		{
			if ((NetMsg)frame[0] == NetMsg.Ping)
			{
				deliveredAt.Add(guest.Clock.NowMs);
			}
		};

		network.SetFaults(HostId, GuestId, new LinkFaults { Down = true });
		var driver = new SimulationDriver(guest.Clock, network, host, guest);

		driver.Tick(1000); // t=1000: attempt fails -> next due at t=2000
		driver.Tick(1000); // t=2000: attempt fails -> doubled delay, next due at t=4000
		driver.Tick(1000); // t=3000: still backing off -> NO send
		network.ClearFaults(HostId, GuestId);
		driver.Tick(1000); // t=4000: backoff elapsed, link healed -> ping arrives
		driver.Tick(1000); // t=5000: success reset the streak -> normal 1 s cadence

		Assert.Equal([4000L, 5000L], deliveredAt);
	}

	[Fact]
	public void Host_WarmupStillReaches_ReachablePeers_OnTheNormalCadence()
	{
		var (network, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];

		var pings = 0;
		guest.Transport.MessageReceived += (_, frame) =>
		{
			if ((NetMsg)frame[0] == NetMsg.Ping)
			{
				pings++;
			}
		};

		var driver = new SimulationDriver(guest.Clock, network, host, guest);
		driver.Tick(1000);
		driver.Tick(1000);
		driver.Tick(1000);

		Assert.True(pings >= 3, $"a healthy peer must keep the existing 1 s warm-up cadence (>= 3 pings in 3 s), got {pings}");
	}
}
