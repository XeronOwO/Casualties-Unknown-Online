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
}
