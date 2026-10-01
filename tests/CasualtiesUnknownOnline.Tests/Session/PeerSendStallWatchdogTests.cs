using System;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The session side of the send-stall policy over the full session stack: the
/// transport's stall edge drops that member with a named reason and leaves the
/// host's own session running — one guest who cannot drain must not take the host
/// down with it — while a guest whose own sends to the host stall ends its own
/// session instead of diverging silently. Both sides leave a notice for the
/// Online UI's status line.
/// </summary>
[Trait("Category", "Integration")]
public class PeerSendStallWatchdogTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void HostStallEdge_DropsTheMember_KeepsTheHostSession_AndPublishesTheName()
	{
		var (host, guest, hostStalls, _) = CreatePair();
		using (host)
		using (guest)
		{
			Assert.Contains(host.Session.Members, member => member.SteamId == GuestId);

			hostStalls.Raise(GuestId);

			Assert.DoesNotContain(host.Session.Members, member => member.SteamId == GuestId);
			Assert.True(host.Session.SessionActive, "the host keeps playing after dropping one guest");
			Assert.False(guest.Session.SessionActive, "the Kicked message reaches a guest whose send path still works");

			var notices = host.Services.GetRequiredService<ISessionNotices>();
			Assert.True(notices.TryTake(out var notice));
			Assert.Contains($"player-{GuestId}", notice.Text);
		}
	}

	[Fact]
	public void GuestStallEdge_EndsTheGuestSession_WithItsOwnNotice()
	{
		var (host, guest, _, guestStalls) = CreatePair();
		using (host)
		using (guest)
		{
			Assert.True(guest.Session.SessionActive);

			guestStalls.Raise(HostId); // the guest's own send path to the host stalled

			Assert.False(guest.Session.SessionActive);
			Assert.True(host.Session.SessionActive, "the host's session does not end with one guest's link");

			var notices = guest.Services.GetRequiredService<ISessionNotices>();
			Assert.True(notices.TryTake(out var notice));
			Assert.Equal(LocalizationCatalog.English["hud.send_stalled"], notice.Text);
		}
	}

	[Fact]
	public void SessionEnd_ResetsTheRefusalState()
	{
		var (host, guest, hostStalls, _) = CreatePair();
		using (host)
		using (guest)
		{
			Assert.Equal(0, hostStalls.Resets);

			host.Session.EndSession();

			Assert.Equal(1, hostStalls.Resets);
		}
	}

	private static (TestNode Host, TestNode Guest, FakeStallSource HostStalls, FakeStallSource GuestStalls) CreatePair()
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var guestSteam = new FakeSteamService(GuestId) { LobbyOwner = HostId, LobbyMembers = [HostId, GuestId] };
		var hostStalls = new FakeStallSource();
		var guestStalls = new FakeStallSource();
		var host = TestNode.Create(HostId, network, hostSteam, clock, pumpFirstFrame: true,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<ISendStallSource>(hostStalls)));
		var guest = TestNode.Create(GuestId, network, guestSteam, clock, pumpFirstFrame: true,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<ISendStallSource>(guestStalls)));

		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);
		return (host, guest, hostStalls, guestStalls);
	}

	/// <summary>The stall edge is the transport's only contract here; the test decides when it fires.</summary>
	private sealed class FakeStallSource : ISendStallSource
	{
		public event Action<ulong>? PeerSendStalled;

		internal int Resets { get; private set; }

		internal void Raise(ulong steamId) => PeerSendStalled?.Invoke(steamId);

		public void ResetRefusals() => Resets++;
	}
}
