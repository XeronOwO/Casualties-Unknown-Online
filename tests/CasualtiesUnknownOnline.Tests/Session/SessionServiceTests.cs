using System.Linq;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The session control-plane decisions (SessionService): the lobby → role
/// mapping, the end-session reset (state cleared, lobby identity kept), the
/// presence checks (a vanished member ends the session; a fresh session must
/// not inherit the "had members" flag) and the scene-state propagation.
/// </summary>
public class SessionServiceTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void LobbyEntered_GuestRole_HostFromLobbyOwner()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		guest.Steam.FireLobbyEntered(LobbyId);

		Assert.Equal(SessionRole.Guest, guest.Session.Role);
		Assert.True(HostId == guest.Session.HostSteamId, "the lobby owner is the host — not the first member");
		Assert.Equal(SessionRole.Host, host.Session.Role);
	}

	[Fact]
	public void LobbyEntered_OwnLobby_Ignored()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		host.Steam.FireLobbyEntered(LobbyId); // the create callback already ran — entering our own lobby is a no-op

		Assert.Equal(SessionRole.Host, host.Session.Role);
		Assert.Equal(HostId, host.Session.HostSteamId);
	}

	[Fact]
	public void EndSession_ResetsState_KeepsLobbyIdentity()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);

		host.Session.EndSession();

		Assert.False(host.Session.SessionActive);
		Assert.Empty(host.Session.Members);
		Assert.Equal(0UL, host.Session.HostSteamId);
		Assert.True(SessionRole.Host == host.Session.Role, "the role follows the lobby identity — a returning guest's handshake still rebuilds everything");
	}

	[Fact]
	public void EndSession_Twice_SecondIsNoOp()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);

		host.Session.EndSession();
		host.Session.EndSession(); // must not throw or double-fire

		Assert.False(host.Session.SessionActive);
	}

	[Fact]
	public void EndSession_FiresSessionEnded()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var ended = 0;
		host.Session.SessionEnded += () => ended++;

		host.Session.EndSession();

		Assert.Equal(1, ended);
	}

	[Fact]
	public void HostPresence_LastGuestLeavesLobby_SessionContinues()
	{
		// The observed chain (2026-08-14): a guest quitting ended the HOST's
		// session ("Members 0, lobby 无效"), and the re-joining guest could
		// never handshake back in — EndSession is irreversible (SessionActive
		// only re-arms on OnLobbyCreated). A guest leaving removes the member;
		// the session continues (the host may be playing alone, and the next
		// guest joins the SAME session).
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var ended = 0;
		host.Session.SessionEnded += () => ended++;

		host.Steam.LobbyMembers = [HostId]; // the guest vanished from the lobby
		host.Update(); // the 2 s presence check — the first pump runs it immediately

		Assert.Empty(host.Session.Members);
		Assert.True(host.Session.SessionActive);
		Assert.True(0 == ended, "a guest leaving must never end the host's session");
	}

	[Fact]
	public void GuestPresence_HostLeavesLobby_EndsSession()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);

		guest.Steam.LobbyMembers = [GuestId]; // the host vanished
		guest.Update();

		Assert.False(guest.Session.SessionActive, "no host migration in the MVP — the session ends");
		Assert.True(SessionRole.Guest == guest.Session.Role, "the lobby identity survives — a rejoining host rebuilds the session");
	}

	[Fact]
	public void ReportSceneState_ReachesThePeer()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);

		host.Session.ReportSceneState(SceneStateType.InWorld, "level1", new NetVector2(3f, 4f));

		Assert.True(host.Session.LocalInWorld);
		Assert.True(guest.Session.IsRemoteInWorld(HostId), "the host's scene report drives the guest's clone presence");
		Assert.Equal(new NetVector2(3f, 4f), guest.Session.GetRemoteSpawnPos(HostId));
	}

	[Fact]
	public void ReportLocalPlayerColor_GuestUpdateReachesHostRoster()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var color = new PlayerColorValue(0.1f, 0.2f, 0.3f, 1f);

		guest.Session.ReportLocalPlayerColor(color);

		var member = host.Session.Members.Single(m => m.SteamId == GuestId);
		Assert.NotNull(member.SelectedColor);
		Assert.Equal(color.R, member.SelectedColor!.Value.R);
		Assert.Equal(color.G, member.SelectedColor.Value.G);
		Assert.Equal(color.B, member.SelectedColor.Value.B);
	}

	[Fact]
	public void ReportLocalPlayerColor_HostUpdateReachesGuestRoster()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var color = new PlayerColorValue(0.4f, 0.5f, 0.6f, 1f);

		host.Session.ReportLocalPlayerColor(color);

		var member = guest.Session.Members.Single(m => m.SteamId == HostId);
		Assert.NotNull(member.SelectedColor);
		Assert.Equal(color.R, member.SelectedColor!.Value.R);
		Assert.Equal(color.G, member.SelectedColor.Value.G);
		Assert.Equal(color.B, member.SelectedColor.Value.B);
	}

	[Fact]
	public void ReportSceneState_WithoutSession_SetsLocalState_NoSend()
	{
		var (_, _, guest) = HandshakeTests.CreateHostAndGuest();

		guest.Session.ReportSceneState(SceneStateType.InWorld, "level1"); // no lobby — the report cannot go anywhere

		Assert.True(guest.Session.LocalInWorld, "the local state is always recorded");
		Assert.False(guest.Session.SessionActive, "the send is gated on the session — nothing leaves without one");
	}

	[Fact]
	public void ReportSceneState_OutOfWorld_ClearsLocalState()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		host.Session.ReportSceneState(SceneStateType.InWorld, "level1");
		Assert.True(guest.Session.IsRemoteInWorld(HostId));

		host.Session.ReportSceneState(SceneStateType.InMenu, "level1");

		Assert.False(host.Session.LocalInWorld);
		Assert.False(guest.Session.IsRemoteInWorld(HostId), "a member leaving the world pauses its clone");
	}
}
