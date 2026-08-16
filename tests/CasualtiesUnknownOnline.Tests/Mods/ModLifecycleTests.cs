using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The mod lifecycle over the real session stack: the first-frame discovery
/// runs Bind → Initialize → Start in that same frame (Update from then on),
/// every stage is exception-isolated (a throwing mod never kills the pump),
/// and the context's Session is a SNAPSHOT taken at bind time — covering the
/// events that fire before discovery (a full handshake in TestNode's default
/// timing completes before any Update) and the host-side SessionActivated
/// that never fires at all.
/// </summary>
public class ModLifecycleTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static TestEchoMod EchoOf(TestNode node) =>
		(TestEchoMod)node.Services.GetRequiredService<ModService>().LoadedMods.Single(m => m is TestEchoMod);

	[Fact]
	public void DiscoveryFrame_RunsBindInitializeStart_ThenUpdatePerFrame()
	{
		// CreatePair pumps the first frame before the handshake — the standard
		// setup, where discovery precedes the session. The discovery frame runs
		// the four stages (Bind/Initialize/Start + the frame's own Update
		// forwarding) in one frame; Update from then on.
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);

		var mod = EchoOf(guest);
		Assert.Equal(["Bind", "Initialize", "Start", "Update"], mod.Lifecycle);
		Assert.Equal(1, mod.UpdateCount);

		guest.Update();
		Assert.Equal(2, mod.UpdateCount);
	}

	[Fact]
	public void Discovery_RunsExactlyOnce_SameInstance()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var first = EchoOf(guest);

		guest.Update(); // a second frame must not re-discover
		Assert.Same(first, EchoOf(guest));
	}

	[Fact]
	public void ThrowingMod_Isolated_OtherModsContinue()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var throwing = (TestThrowingMod)guest.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestThrowingMod);

		// The discovery frame's own Update already threw once (isolated) — the
		// pump survives and the sibling mod keeps ticking.
		Assert.Equal(1, throwing.UpdateAttempts);
		Assert.Equal(1, EchoOf(guest).UpdateCount);

		guest.Update();
		Assert.Equal(2, throwing.UpdateAttempts);
		Assert.Equal(2, EchoOf(guest).UpdateCount);
	}

	[Fact]
	public void BindAfterFullHandshake_SnapshotShowsActiveSessionAndMembers()
	{
		// A guest that auto-joins at startup (+connect_lobby) can complete its
		// handshake BEFORE its first frame — its mods bind later with a snapshot
		// of the already-live session (the events fired before discovery are
		// covered by the snapshot). The host's mod control is stubbed with no
		// requirements so the guest's empty pre-discovery list is admitted.
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var guestSteam = new FakeSteamService(GuestId) { LobbyOwner = HostId, LobbyMembers = [HostId, GuestId] };
		var host = TestNode.Create(HostId, network, hostSteam, clock, pumpFirstFrame: true,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModsControl>(new StubModsControl([]))));
		var guest = TestNode.Create(GuestId, network, guestSteam, clock); // NOT pumped — discovery happens after the handshake
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId); // full handshake, no Update yet

		guest.Update(); // the discovery frame — bind happens NOW

		var session = EchoOf(guest).Context!.Session;
		Assert.True(session.SessionActive, "the bind-time snapshot must see the completed handshake");
		Assert.False(session.IsHost);
		Assert.Equal(GuestId, session.LocalSteamId);
		Assert.Equal(HostId, session.HostSteamId);
		// MemberSteamIds is the peer member set (the local peer has its own field) —
		// the same semantics as the session's Broadcast fan-out.
		Assert.Equal([HostId], session.MemberSteamIds);
	}

	[Fact]
	public void HostBindAfterLobbyCreation_SnapshotShowsHostActive()
	{
		// The host side never fires SessionActivated (it activated at lobby
		// creation, not via a handshake) — the snapshot is how a host mod learns
		// the session is live.
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var host = TestNode.Create(HostId, network, hostSteam, clock);
		host.Steam.FireLobbyCreated(LobbyId);

		host.Update(); // discovery after the lobby exists

		var session = EchoOf(host).Context!.Session;
		Assert.True(session.SessionActive);
		Assert.True(session.IsHost);
		Assert.Equal(HostId, session.HostSteamId);
	}

	[Fact]
	public void GuestOutsideSession_SnapshotShowsInactive()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		// CreatePair's discovery frame ran before the lobby — inactive snapshot.
		var session = EchoOf(guest).Context!.Session;
		Assert.False(session.SessionActive);
	}

	[Fact]
	public void SessionEnded_FiresToEveryMod()
	{
		// CreatePair already created the host lobby — the host session exists.
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var ended = 0;
		EchoOf(host).Context!.SessionEnded += () => ended++;

		host.Session.EndSession(); // internal, InternalsVisibleTo

		Assert.Equal(1, ended);
	}

	[Fact]
	public void PlayerJoined_FiresPerMemberAfterDiscovery()
	{
		// g2 joins AFTER the discovery frame — its MemberAdded is a live event
		// (the earlier g1 join happened before discovery and is covered by the
		// snapshot instead).
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var g1Steam = new FakeSteamService(1002) { LobbyOwner = HostId, LobbyMembers = [HostId, 1002] };
		var host = TestNode.Create(HostId, network, hostSteam, clock, pumpFirstFrame: true);
		var g1 = TestNode.Create(1002, network, g1Steam, clock, pumpFirstFrame: true);
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, 1002];
		g1.Steam.FireLobbyEntered(LobbyId);

		var joined = new List<ulong>();
		EchoOf(host).Context!.PlayerJoined += id => joined.Add(id);

		// g2 joins late — after the mods are already bound.
		var g2Steam = new FakeSteamService(GuestId) { LobbyOwner = HostId, LobbyMembers = [HostId, 1002, GuestId] };
		var g2 = TestNode.Create(GuestId, network, g2Steam, clock, pumpFirstFrame: true);
		host.Steam.LobbyMembers = [HostId, 1002, GuestId];
		g2.Steam.FireLobbyEntered(LobbyId);

		Assert.Equal([GuestId], joined);
	}

	[Fact]
	public void PlayerLeft_FiresWhenHostRemovesMember()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var left = 0ul;
		EchoOf(host).Context!.PlayerLeft += id => left = id;

		((Runtime.Session.ISessionControl)host.Session).RemoveGuestMember(GuestId);

		Assert.Equal(GuestId, left);
	}

	[Fact]
	public void StopAndDispose_ReverseOrder_PerMod()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var mod = EchoOf(guest); // take the reference before the container dies

		guest.Dispose(); // TestNode.Dispose runs Stop then the container disposes

		Assert.Equal("Stop", mod.Lifecycle[mod.Lifecycle.Count - 2]);
		Assert.Equal("Dispose", mod.Lifecycle[mod.Lifecycle.Count - 1]); // the idempotent dispose runs once
	}

	// A host-side mod control with no requirements — the guest's pre-discovery
	// (empty) list is admitted, so the "bind after handshake" timing is reachable.
	private sealed class StubModsControl : IModsControl
	{
		private readonly List<ModManifest> _manifests;

		internal StubModsControl(List<ModManifest> manifests)
		{
			_manifests = manifests;
		}

		public void FireModMessageReceived(ulong sender, Runtime.Protocol.Messages.ModMessageMsg msg)
		{
		}

		public void FireModCommandRequestReceived(ulong sender, Runtime.Protocol.Messages.ModCommandRequestMsg msg)
		{
		}

		public void FireModCommandResultReceived(ulong sender, Runtime.Protocol.Messages.ModCommandResultMsg msg)
		{
		}

		public IReadOnlyList<ModManifest> CurrentModManifests => _manifests;

		public bool IsDiscoveryComplete => true;
	}
}
