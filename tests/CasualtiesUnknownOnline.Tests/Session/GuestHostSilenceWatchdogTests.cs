using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The guest half of the send-stall policy over the full session stack: a guest
/// that stops hearing the host ends its own session (with a notice) instead of
/// sitting in a world that stopped moving — and normal traffic never trips it.
/// </summary>
[Trait("Category", "Integration")]
public class GuestHostSilenceWatchdogTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static (SimulationDriver Driver, TestNode Host, TestNode Guest) OpenSession()
	{
		var (network, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);
		return (new SimulationDriver(guest.Clock, network, host, guest), host, guest);
	}

	[Fact]
	public void HostGoesSilent_GuestEndsItsOwnSession_WithANotice()
	{
		var (driver, host, guest) = OpenSession();
		using (host)
		using (guest)
		{
			Assert.True(guest.Session.SessionActive);
			driver.Network.SetFaults(HostId, GuestId, new LinkFaults { Down = true });

			driver.TickUntil(() => !guest.Session.SessionActive, maxMs: 20_000);

			Assert.False(guest.Session.SessionActive);
			var notices = guest.Services.GetRequiredService<ISessionNotices>();
			Assert.True(notices.TryTake(out var notice));
			Assert.Equal(LocalizationCatalog.English["hud.host_silent"], notice.Text);
		}
	}

	[Fact]
	public void NormalTraffic_NeverTripsTheWatchdog()
	{
		var (driver, host, guest) = OpenSession();
		using (host)
		using (guest)
		{
			for (var elapsed = 0L; elapsed < 20_000; elapsed += 100)
			{
				driver.Tick(100);
			}

			Assert.True(guest.Session.SessionActive, "the host's 5 s ping cadence keeps the guest alive");
			Assert.False(guest.Services.GetRequiredService<ISessionNotices>().TryTake(out _));
		}
	}
}
