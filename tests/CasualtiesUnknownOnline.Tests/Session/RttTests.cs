using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The ping/pong diagnostics loop: RequestPing → peer echoes the tick → the
/// requester records the RTT. Full round-trip over the fake network.
/// </summary>
public class RttTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void RequestPing_UpdatesLastRtt()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		Assert.Equal(-1f, host.Session.LastRttMs); // no ping yet

		host.Session.RequestPing();

		Assert.True(host.Session.LastRttMs >= 0f, $"RTT must be recorded, was {host.Session.LastRttMs}");
	}
}
