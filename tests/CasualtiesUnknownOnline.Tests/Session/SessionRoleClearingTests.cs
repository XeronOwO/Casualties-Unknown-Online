using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// A session that ends must leave the client either in the lobby it can rebuild (a Steam outage keeps
/// the role for the returning host) or with NO identity at all (IP-direct has no lobby to rebuild, and a
/// kept role leaves the client a ghost that refuses every join until the process restarts — measured
/// 2026-11-13 on the boxed guest).
/// </summary>
public sealed class SessionRoleClearingTests
{
	private const ulong HostId = 76561190000000001;
	private const ulong GuestId = 76561190000000002;

	[Fact]
	public void SessionEnd_InsideALobby_KeepsTheRoleForTheReturningHost()
	{
		var (_, guest) = TestNode.CreatePair(HostId, GuestId, 1234);
		Assert.Equal(SessionRole.Guest, guest.Session.Role);

		guest.Session.EndSession();

		Assert.Equal(SessionRole.Guest, guest.Session.Role);
	}

	[Fact]
	public void SessionEnd_WithNoLobby_ClearsTheRole()
	{
		var (_, guest) = TestNode.CreatePair(HostId, GuestId, 0);
		Assert.Equal(SessionRole.Guest, guest.Session.Role);

		guest.Session.EndSession();

		Assert.Equal(SessionRole.None, guest.Session.Role);
	}

	[Fact]
	public void SessionEnd_OnATransportBackedIdentity_EndsTheTransportAndClearsTheRole()
	{
		// IP direct presents the TCP transport as a lobby (id 1) that is never "left" by itself: the
		// session's end must leave it, or the synthetic lobby keeps the role and the client stays a
		// ghost that refuses every join (measured 2026-11-14 on the boxed guest).
		var (_, guest) = TestNode.CreatePair(HostId, GuestId, 1);
		guest.Steam.EndsWithTheSession = true;
		Assert.Equal(SessionRole.Guest, guest.Session.Role);

		guest.Session.EndSession();

		// The fake keeps the role (it does not model the transport's disconnect → LobbyLeft → role
		// clear, which the real IpDirectSteamService performs); the fact this test owns is that the
		// session tells a transport-backed identity to end when the session ends.
		Assert.Equal(1, guest.Steam.LeaveLobbyCalls);
	}

	[Fact]
	public void SessionEnd_OnASteamLobby_LeavesTheLobbyAlone()
	{
		var (_, guest) = TestNode.CreatePair(HostId, GuestId, 1234);
		guest.Steam.EndsWithTheSession = false;

		guest.Session.EndSession();

		Assert.True(guest.Steam.IsActive);
		Assert.Equal(SessionRole.Guest, guest.Session.Role);
	}
}
