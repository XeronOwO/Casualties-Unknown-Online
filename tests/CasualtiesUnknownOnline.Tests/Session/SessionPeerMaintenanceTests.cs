using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// A guest's lobby read is a SAMPLE, not a verdict: `CheckPeerPresence` used to end the whole session on one reading that did not name the host, and a single
/// stale snapshot right after a re-join did exactly that while the host still held the lobby (measured 2026-10-28: the guest logged "Host left the lobby" and
/// never re-joined, retrying handshakes into a session it had already dropped). Ending a session is irreversible — `SessionActive` re-arms only on
/// `OnLobbyCreated` — so the decision now needs the host to stay absent for consecutive checks, and an EMPTY reading never counts at all.
/// </summary>
public sealed class SessionPeerMaintenanceTests
{
	private const ulong HostId = 90000000000000001;
	private const ulong GuestId = 90000000000000002;
	private const ulong AltId = 90000000000000003;

	[Fact]
	public void AGuest_KeepsItsSessionWhenOneReadingOmitsTheHost()
	{
		var (maintenance, clock, steam, ended) = Create();

		steam.LobbyMembers = [GuestId]; // a snapshot that names everyone but the host: Steam is mid-update
		clock.Advance(2000);
		maintenance.CheckPeerPresence();

		Assert.Equal(0, ended.Count);
	}

	[Fact]
	public void AGuest_KeepsItsSessionWhenTheReadingNamesNobody()
	{
		var (maintenance, clock, steam, ended) = Create();

		steam.LobbyMembers = []; // not populated yet — says nothing about the host
		clock.Advance(2000);
		maintenance.CheckPeerPresence();
		clock.Advance(2000);
		maintenance.CheckPeerPresence();
		clock.Advance(2000);
		maintenance.CheckPeerPresence();

		Assert.Equal(0, ended.Count);
	}

	[Fact]
	public void AGuest_EndsItsSessionOnceTheHostIsAbsentOnConsecutiveChecks()
	{
		var (maintenance, clock, steam, ended) = Create();

		steam.LobbyMembers = [GuestId];
		for (var check = 0; check < 3; check++)
		{
			clock.Advance(2000);
			maintenance.CheckPeerPresence();
		}

		Assert.Single(ended);
	}

	[Fact]
	public void AGuest_ForgetsTheAbsenceWhenTheHostComesBackInBetween()
	{
		var (maintenance, clock, steam, ended) = Create();

		steam.LobbyMembers = [GuestId];
		clock.Advance(2000);
		maintenance.CheckPeerPresence();
		clock.Advance(2000);
		maintenance.CheckPeerPresence();

		steam.LobbyMembers = [HostId, GuestId]; // a reading that names the host clears the streak
		clock.Advance(2000);
		maintenance.CheckPeerPresence();

		steam.LobbyMembers = [GuestId];
		clock.Advance(2000);
		maintenance.CheckPeerPresence();

		Assert.Equal(0, ended.Count);
	}

	/// <summary>
	/// A host's lobby read is the same kind of SAMPLE. Dropping a member on one reading that omits them is not quiet bookkeeping: every other member gets
	/// a `PlayerLeave` for a player who is still playing, their presence row is gone, and `IsRemoteInWorld` refuses every third-party gesture afterwards
	/// (measured 2026-11-01: an alt could not open the medical view of a guest the host was still treating). The host therefore needs the same corroboration
	/// the guest side already demanded of its own reading, and an EMPTY reading never counts.
	/// </summary>
	[Fact]
	public void AHost_KeepsAMemberWhenOneReadingOmitsIt()
	{
		var (maintenance, clock, steam, removed) = CreateHost();

		steam.LobbyMembers = [HostId, AltId]; // a snapshot Steam is still populating: it names everyone but the member
		clock.Advance(2000);
		maintenance.CheckPeerPresence();

		Assert.Empty(removed);
	}

	[Fact]
	public void AHost_KeepsAMemberWhenTheReadingNamesNobody()
	{
		var (maintenance, clock, steam, removed) = CreateHost();

		steam.LobbyMembers = [];
		for (var check = 0; check < 4; check++)
		{
			clock.Advance(2000);
			maintenance.CheckPeerPresence();
		}

		Assert.Empty(removed);
	}

	[Fact]
	public void AHost_RemovesAMemberOnlyOnceConsecutiveReadingsAgree()
	{
		var (maintenance, clock, steam, removed) = CreateHost();

		steam.LobbyMembers = [HostId, GuestId]; // a reading that names the member clears the streak
		clock.Advance(2000);
		maintenance.CheckPeerPresence();

		steam.LobbyMembers = [HostId]; // the member really is gone: no reading names it any more
		clock.Advance(2000);
		maintenance.CheckPeerPresence();
		Assert.Empty(removed);

		clock.Advance(2000);
		maintenance.CheckPeerPresence();
		Assert.Empty(removed);

		clock.Advance(2000);
		maintenance.CheckPeerPresence();
		Assert.Single(removed);
		Assert.Equal(GuestId, removed[0]);
	}

	[Fact]
	public void AHost_ForgetsTheAbsenceWhenTheMemberComesBackInBetween()
	{
		var (maintenance, clock, steam, removed) = CreateHost();

		steam.LobbyMembers = [HostId];
		clock.Advance(2000);
		maintenance.CheckPeerPresence();
		clock.Advance(2000);
		maintenance.CheckPeerPresence();

		steam.LobbyMembers = [HostId, GuestId]; // the reading that named it again clears the streak
		clock.Advance(2000);
		maintenance.CheckPeerPresence();

		steam.LobbyMembers = [HostId];
		clock.Advance(2000);
		maintenance.CheckPeerPresence();
		clock.Advance(2000);
		maintenance.CheckPeerPresence();

		Assert.Empty(removed);
	}

	private static (SessionPeerMaintenance Maintenance, FakeClock Clock, FakeSteamService Steam, List<ulong> Removed) CreateHost()
	{
		var clock = new FakeClock();
		var steam = new FakeSteamService(HostId) { LobbyMembers = [] };
		var identity = new SessionIdentity { Role = SessionRole.Host, HostSteamId = HostId };
		var state = new SessionState { SessionActive = true };
		var presence = new MemberPresenceTable();
		presence.GetOrCreateMember(GuestId).Handshaken = true;
		var removed = new List<ulong>();
		var maintenance = new SessionPeerMaintenance(
			steam,
			new PacketSender(new NullTransport(), new NetworkTrafficMonitor(clock, NullLogger<NetworkTrafficMonitor>.Instance)),
			clock,
			new EmptyModListProvider(),
			identity,
			new LocalPeerIdentity("test-game", "test-cuo", 1),
			state,
			presence,
			new PeerWarmupBackoff(),
			(member, _) => removed.Add(member),
			() => { },
			NullLogger.Instance);
		return (maintenance, clock, steam, removed);
	}

	private static (SessionPeerMaintenance Maintenance, FakeClock Clock, FakeSteamService Steam, List<string> Ended) Create()
	{
		var clock = new FakeClock();
		var steam = new FakeSteamService(GuestId) { LobbyMembers = [] };
		var identity = new SessionIdentity { Role = SessionRole.Guest, HostSteamId = HostId };
		var state = new SessionState { SessionActive = true };
		var presence = new MemberPresenceTable();
		var ended = new List<string>();
		var maintenance = new SessionPeerMaintenance(
			steam,
			new PacketSender(new NullTransport(), new NetworkTrafficMonitor(clock, NullLogger<NetworkTrafficMonitor>.Instance)),
			clock,
			new EmptyModListProvider(),
			identity,
			new LocalPeerIdentity("test-game", "test-cuo", 1),
			state,
			presence,
			new PeerWarmupBackoff(),
			(_, reason) => { },
			() => ended.Add("ended"),
			NullLogger.Instance);
		return (maintenance, clock, steam, ended);
	}

	private sealed class NullTransport : INetworkTransport
	{
		public event Action<ulong, byte[]>? MessageReceived { add { } remove { } }

		public bool SendTo(ulong steamId, byte[] data, bool reliable) => true;

		public void Poll()
		{
		}
	}

	private sealed class EmptyModListProvider : IModListProvider
	{
		public List<Runtime.Protocol.Messages.ModInfoMsg> CurrentModInfos() => [];
	}
}
