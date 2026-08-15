using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.DependencyInjection;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Lobby-domain transitions over the full production session stack (fake
/// network + fake Steam). The lobby identity is a real state machine now:
/// leaving a lobby ends the session and drops the role; entering another
/// player's lobby rebinds as Guest and handshakes from scratch.
/// </summary>
public class LobbySwitchTests
{
	private const ulong OldHostId = 1001;
	private const ulong SwitcherId = 2001;
	private const ulong NewHostId = 3001;
	private const ulong OldGuestId = 4001;
	private const ulong OldLobbyId = 8001;
	private const ulong NewLobbyId = 9001;

	private static bool Handshaken(TestNode host, TestNode guest) =>
		host.Session.Members.Any(m => m.SteamId == guest.SteamId && m.Handshaken)
		&& guest.Session.Members.Any(m => m.SteamId == host.SteamId && m.Handshaken);

	[Fact]
	public void HostedOwnLobby_ThenEnteredAnotherHostsLobby_RebindsAsGuestAndHandshakes()
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var oldGuest = TestNode.Create(OldGuestId, network, new FakeSteamService(OldGuestId)
		{ LobbyOwner = SwitcherId, LobbyMembers = [SwitcherId, OldGuestId] }, clock, pumpFirstFrame: true);
		var switcher = TestNode.Create(SwitcherId, network, new FakeSteamService(SwitcherId)
		{ LobbyOwner = SwitcherId, LobbyMembers = [SwitcherId, OldGuestId] }, clock, pumpFirstFrame: true);
		var newHost = TestNode.Create(NewHostId, network, new FakeSteamService(NewHostId)
		{ LobbyOwner = NewHostId, LobbyMembers = [NewHostId, SwitcherId] }, clock, pumpFirstFrame: true);

		switcher.Steam.FireLobbyCreated(OldLobbyId); // F8: the switcher hosted its own lobby
		oldGuest.Steam.FireLobbyEntered(OldLobbyId); // ... and had a guest
		Assert.True(Handshaken(switcher, oldGuest), "the old lobby session must be established first");
		newHost.Steam.FireLobbyCreated(NewLobbyId);

		var ended = 0;
		switcher.Session.SessionEnded += () => ended++;
		switcher.Steam.LobbyOwner = NewHostId;
		switcher.Steam.LobbyMembers = [NewHostId, SwitcherId];
		switcher.Steam.FireLobbyLeft(OldLobbyId);
		Assert.True(switcher.Session.Role == SessionRole.None, "leaving the old lobby drops the identity");
		Assert.False(switcher.Session.SessionActive, "the old host session is gone");
		Assert.Empty(switcher.Session.Members);

		switcher.Steam.FireLobbyEntered(NewLobbyId);
		switcher.Update();
		newHost.Update();

		Assert.True(switcher.Session.Role == SessionRole.Guest, "the switcher must become a guest of the new lobby");
		Assert.True(switcher.Session.HostSteamId == NewHostId, "the new lobby owner is the new host");
		Assert.True(1 == ended, "the old session ended exactly once");
		Assert.True(Handshaken(newHost, switcher), "the switched guest must handshake end-to-end with the new host");
		Assert.False(switcher.Session.Members.Any(m => m.SteamId == OldGuestId), "the old guest presence must not leak into the new lobby");
	}

	[Fact]
	public void GuestSwitchingToAnotherHostsLobby_ClearsOldPresenceAndHandshakes()
	{
		var (oldHost, switcher) = TestNode.CreatePair(OldHostId, SwitcherId, OldLobbyId);
		var newHost = TestNode.Create(NewHostId, switcher.Transport.Network, new FakeSteamService(NewHostId)
		{ LobbyOwner = NewHostId, LobbyMembers = [NewHostId, SwitcherId] }, switcher.Clock, pumpFirstFrame: true);
		newHost.Steam.FireLobbyCreated(NewLobbyId);
		Assert.True(Handshaken(oldHost, switcher), "the old session must be established first");

		var ended = 0;
		switcher.Session.SessionEnded += () => ended++;
		switcher.Steam.LobbyOwner = NewHostId;
		switcher.Steam.LobbyMembers = [NewHostId, SwitcherId];
		switcher.Steam.FireLobbyLeft(OldLobbyId);
		switcher.Steam.FireLobbyEntered(NewLobbyId);
		switcher.Update();
		newHost.Update();

		Assert.True(switcher.Session.Role == SessionRole.Guest, "the role stays Guest but must rebind to the new lobby");
		Assert.True(switcher.Session.HostSteamId == NewHostId);
		Assert.True(1 == ended, "the old session ended exactly once");
		Assert.True(Handshaken(newHost, switcher), "the switched guest handshakes the new host");
		Assert.False(switcher.Session.Members.Any(m => m.SteamId == OldHostId), "the old host presence must be gone");
	}

	[Fact]
	public void GuestCreatingOwnLobby_BecomesHost_WithFreshSession()
	{
		var (_, switcher) = TestNode.CreatePair(OldHostId, SwitcherId, OldLobbyId);

		var ended = 0;
		switcher.Session.SessionEnded += () => ended++;
		switcher.Steam.LobbyOwner = SwitcherId;
		switcher.Steam.LobbyMembers = [SwitcherId];
		switcher.Steam.FireLobbyLeft(OldLobbyId);
		switcher.Steam.FireLobbyCreated(NewLobbyId);

		Assert.True(switcher.Session.Role == SessionRole.Host, "the lobby creator is the host of the new lobby");
		Assert.True(switcher.Session.HostSteamId == SwitcherId);
		Assert.True(switcher.Session.SessionActive, "the host is authoritative from lobby creation");
		Assert.True(1 == ended, "the old guest session ended exactly once");
		Assert.Empty(switcher.Session.Members);
	}

	[Fact]
	public void HostRecreatingLobby_KeepsRoleButStartsFreshSession()
	{
		var (host, _) = TestNode.CreatePair(OldHostId, SwitcherId, OldLobbyId);

		var ended = 0;
		host.Session.SessionEnded += () => ended++;
		host.Steam.LobbyOwner = OldHostId;
		host.Steam.LobbyMembers = [OldHostId];
		host.Steam.FireLobbyLeft(OldLobbyId);
		host.Steam.FireLobbyCreated(NewLobbyId);

		Assert.True(host.Session.Role == SessionRole.Host);
		Assert.True(host.Session.HostSteamId == OldHostId);
		Assert.True(host.Session.SessionActive);
		Assert.True(1 == ended);
		Assert.Empty(host.Session.Members);
	}

	[Fact]
	public void LobbyLeft_AloneEndsSessionAndDropsRole()
	{
		var (_, switcher) = TestNode.CreatePair(OldHostId, SwitcherId, OldLobbyId);

		switcher.Steam.FireLobbyLeft(OldLobbyId);

		Assert.True(switcher.Session.Role == SessionRole.None, "no lobby means no lobby identity");
		Assert.False(switcher.Session.SessionActive);
		Assert.Empty(switcher.Session.Members);
	}

	[Fact]
	public void HostToGuestSwitch_ThenHostReleasesStartGate_WorldReadyArrives()
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var host = TestNode.Create(OldHostId, network, new FakeSteamService(OldHostId)
		{ LobbyOwner = OldHostId, LobbyMembers = [OldHostId] }, clock, pumpFirstFrame: true);
		var switcher = TestNode.Create(SwitcherId, network, new FakeSteamService(SwitcherId)
		{ LobbyOwner = SwitcherId, LobbyMembers = [SwitcherId] }, clock, pumpFirstFrame: true);

		host.Steam.FireLobbyCreated(NewLobbyId);
		host.Steam.LobbyMembers = [OldHostId, SwitcherId];
		switcher.Steam.FireLobbyCreated(OldLobbyId); // the bug scenario: the switcher first hosted its own lobby
		switcher.Steam.LobbyOwner = OldHostId;
		switcher.Steam.LobbyMembers = [OldHostId, SwitcherId];
		switcher.Steam.FireLobbyLeft(OldLobbyId);
		switcher.Steam.FireLobbyEntered(NewLobbyId);
		switcher.Update();
		host.Update();

		var switcherWorld = switcher.Services.GetRequiredService<WorldService>();
		var hostWorld = host.Services.GetRequiredService<WorldService>();
		var ready = 0;
		switcherWorld.WorldReadyReceived += () => ready++;

		host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(1f, 2f));
		Assert.True(hostWorld.StartStartGate(), "the switched guest is still loading — the gate arms");
		switcher.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(3f, 4f));

		Assert.True(1 == ready, "the switched guest must receive WorldReady once the host releases the gate");
	}

	[Fact]
	public void DuplicateEnteredEvent_SameActiveSession_DoesNotTearDownAgain()
	{
		var (host, switcher) = TestNode.CreatePair(OldHostId, SwitcherId, OldLobbyId);

		var ended = 0;
		switcher.Session.SessionEnded += () => ended++;
		switcher.Steam.FireLobbyEntered(OldLobbyId); // Steam re-fires the entered callback for the same lobby

		Assert.True(0 == ended, "a duplicate same-lobby entered callback is an idempotent re-handshake, not a session end");
		Assert.True(switcher.Session.Role == SessionRole.Guest);
		Assert.True(Handshaken(host, switcher), "the re-handshake completes without rebuilding the presence table");
	}
}
