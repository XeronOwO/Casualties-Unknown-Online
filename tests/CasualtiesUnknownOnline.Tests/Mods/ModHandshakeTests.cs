using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The Phase 4 Mod API consistency check, driven over the real handshake with
/// stubbed control surfaces (each node's declared mods are fully controlled —
/// the same test assembly is shared by every node, so the real discovery could
/// not produce asymmetric lists): RequiresAllPlayers/Synchronized/Authoritative
/// missing on either side or version-unequal rejects the handshake BEFORE the
/// member is created; ClientOnly/Cosmetic differences and host-only mods pass;
/// a malformed member list (empty/duplicated id, Unspecified or unknown
/// NetworkMode) is rejected; a handshake arriving before the discovery scan is
/// refused as "pending" and passes on the retry.
/// </summary>
public class ModHandshakeTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	// The matrix's one mod id — declared per test with the mode/version needed.
	private const string ModId = "test.matrix";

	private static ModManifest Manifest(NetworkMode mode, string version = "1.0.0", ModPermission permissions = ModPermission.None) =>
		new(ModId, "Matrix Mod", version, mode, null, permissions);

	private static ModInfoMsg Info(NetworkMode mode, string version = "1.0.0", ModPermission permissions = ModPermission.None) =>
		new() { Id = ModId, Version = version, NetworkMode = mode, Permissions = permissions };

	private static (TestNode Host, TestNode Guest) CreatePair(
		List<ModManifest> hostMods, List<ModInfoMsg> guestInfos, bool hostComplete = true)
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var guestSteam = new FakeSteamService(GuestId) { LobbyOwner = HostId, LobbyMembers = [HostId, GuestId] };
		var hostControl = new StubModsControl(hostMods, hostComplete);
		var guestControl = new StubModsControl([]);
		var guestProvider = new StubModListProvider(guestInfos);
		var host = TestNode.Create(HostId, network, hostSteam, clock, pumpFirstFrame: true,
			extraRegistrations: s =>
			{
				s.Replace(ServiceDescriptor.Singleton<IModsControl>(hostControl));
				s.Replace(ServiceDescriptor.Singleton<IModListProvider>(guestProvider));
			});
		var guest = TestNode.Create(GuestId, network, guestSteam, clock, pumpFirstFrame: true,
			extraRegistrations: s =>
			{
				s.Replace(ServiceDescriptor.Singleton<IModsControl>(guestControl));
				s.Replace(ServiceDescriptor.Singleton<IModListProvider>(guestProvider));
			});
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);
		return (host, guest);
	}

	private static bool GuestHandshaken(TestNode host) =>
		host.Session.Members.Any(m => m.SteamId == GuestId && m.Handshaken);

	// ---- The matrix rows ----

	[Fact]
	public void StateBearingModMissingOnGuest_Rejected_NoMemberCreated()
	{
		var (host, _) = CreatePair([Manifest(NetworkMode.RequiresAllPlayers)], []);

		Assert.False(GuestHandshaken(host), "a member missing a RequiresAllPlayers mod must not be admitted");
		Assert.Empty(host.Session.Members);
	}

	[Fact]
	public void SynchronizedModMissingOnGuest_Rejected()
	{
		var (host, _) = CreatePair([Manifest(NetworkMode.Synchronized)], []);

		Assert.False(GuestHandshaken(host));
	}

	[Fact]
	public void AuthoritativeModMissingOnGuest_Rejected()
	{
		var (host, _) = CreatePair([Manifest(NetworkMode.Authoritative)], []);

		Assert.False(GuestHandshaken(host));
	}

	[Fact]
	public void StateBearingModPresentAndEqual_Accepted()
	{
		var (host, _) = CreatePair([Manifest(NetworkMode.RequiresAllPlayers)], [Info(NetworkMode.RequiresAllPlayers)]);

		Assert.True(GuestHandshaken(host));
	}

	[Fact]
	public void VersionMismatchOnStateBearingMod_Rejected()
	{
		var (host, _) = CreatePair([Manifest(NetworkMode.RequiresAllPlayers)], [Info(NetworkMode.RequiresAllPlayers, "0.9.0")]);

		Assert.False(GuestHandshaken(host));
	}

	[Fact]
	public void SemverBuildMetadataDifference_Accepted()
	{
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.Synchronized, "1.0.0+host.1")],
			[Info(NetworkMode.Synchronized, "1.0.0+guest.2")]);

		Assert.True(GuestHandshaken(host), "build metadata does not affect SemVer precedence");
	}

	[Fact]
	public void SemverPrereleaseDifference_Rejected()
	{
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.Synchronized, "1.0.0")],
			[Info(NetworkMode.Synchronized, "1.0.0-alpha")]);

		Assert.False(GuestHandshaken(host), "a prerelease and its release differ by precedence");
	}

	[Fact]
	public void MalformedSemVerInGuestList_Rejected()
	{
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.Synchronized, "1.0.0")],
			[Info(NetworkMode.Synchronized, "not-semver")]);

		Assert.False(GuestHandshaken(host));
	}

	[Fact]
	public void PermissionMismatchOnStateBearingMod_Rejected()
	{
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.Synchronized, permissions: ModPermission.RegisterCommand)],
			[Info(NetworkMode.Synchronized)]);

		Assert.False(GuestHandshaken(host), "state-bearing mod copies must declare the same permissions");
	}

	[Fact]
	public void GuestDeclaresDifferentModeForHostStateBearingMod_Rejected()
	{
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.Synchronized)],
			[Info(NetworkMode.ClientOnly)]);

		Assert.False(GuestHandshaken(host), "the network contract must match for the same mod id");
	}

	[Fact]
	public void GuestClaimsStateBearingForHostClientOnlyMod_Rejected()
	{
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.ClientOnly)],
			[Info(NetworkMode.Synchronized)]);

		Assert.False(GuestHandshaken(host), "the host cannot arbitrate a Synchronized contract it never declared");
	}

	[Fact]
	public void UnknownPermissionBitsInGuestList_Rejected()
	{
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.Synchronized)],
			[Info(NetworkMode.Synchronized, permissions: (ModPermission)(1 << 20))]);

		Assert.False(GuestHandshaken(host));
	}

	[Fact]
	public void ClientOnlyMissingOnGuest_Accepted()
	{
		var (host, _) = CreatePair([Manifest(NetworkMode.ClientOnly)], []);

		Assert.True(GuestHandshaken(host), "a ClientOnly mod is a local surface — its absence must not block");
	}

	[Fact]
	public void CosmeticVersionDifference_Accepted()
	{
		var (host, _) = CreatePair([Manifest(NetworkMode.Cosmetic, "1.0.0")], [Info(NetworkMode.Cosmetic, "2.0.0")]);

		Assert.True(GuestHandshaken(host));
	}

	[Fact]
	public void HostOnlyMod_IsHostSideOnly()
	{
		var (host, _) = CreatePair([Manifest(NetworkMode.HostOnly)], []);

		Assert.True(GuestHandshaken(host), "a HostOnly mod is the host's own — a guest lacking it must pass");
	}

	[Fact]
	public void GuestClaimsStateBearingModHostLacks_Rejected()
	{
		var (host, _) = CreatePair([], [Info(NetworkMode.Synchronized)]);

		Assert.False(GuestHandshaken(host), "a guest claiming a Synchronized mod the host cannot arbitrate must not be admitted");
	}

	[Fact]
	public void GuestClaimsAuthoritativeModHostLacks_Rejected()
	{
		var (host, _) = CreatePair([], [Info(NetworkMode.Authoritative)]);

		Assert.False(GuestHandshaken(host));
	}

	[Fact]
	public void GuestClaimsClientOnlyModHostLacks_Accepted()
	{
		var (host, _) = CreatePair([], [Info(NetworkMode.ClientOnly)]);

		Assert.True(GuestHandshaken(host), "a local-surface mod the host lacks is the host's business, not a member's");
	}

	// ---- The malformed-list shape checks ----

	[Fact]
	public void DuplicatedIdInGuestList_Rejected()
	{
		var (host, _) = CreatePair([Manifest(NetworkMode.RequiresAllPlayers)], [Info(NetworkMode.RequiresAllPlayers), Info(NetworkMode.RequiresAllPlayers)]);

		Assert.False(GuestHandshaken(host));
	}

	[Fact]
	public void EmptyIdInGuestList_Rejected()
	{
		var (host, _) = CreatePair([Manifest(NetworkMode.RequiresAllPlayers)], [Info(NetworkMode.RequiresAllPlayers), new ModInfoMsg { Id = "", Version = "1.0.0", NetworkMode = NetworkMode.ClientOnly }]);

		Assert.False(GuestHandshaken(host));
	}

	[Fact]
	public void UnspecifiedNetworkModeInGuestList_Rejected()
	{
		var (host, _) = CreatePair([Manifest(NetworkMode.RequiresAllPlayers)], [Info(NetworkMode.Unspecified)]);

		Assert.False(GuestHandshaken(host));
	}

	[Fact]
	public void UnknownNetworkModeValueInGuestList_Rejected()
	{
		var (host, _) = CreatePair([Manifest(NetworkMode.RequiresAllPlayers)], [new ModInfoMsg { Id = ModId, Version = "1.0.0", NetworkMode = (NetworkMode)99 }]);

		Assert.False(GuestHandshaken(host));
	}

	[Fact]
	public void BothSidesEmpty_Accepted()
	{
		var (host, _) = CreatePair([], []);

		Assert.True(GuestHandshaken(host));
	}

	// ---- The discovery window ----

	[Fact]
	public void HandshakeBeforeDiscovery_RefusedThenRetrySucceeds()
	{
		// The discovery scan has not run (stub says incomplete) — the check is
		// refused ("pending"), the guest's 1 s handshake retry re-runs it once
		// the discovery completes. The stub flips to complete to model that.
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var guestSteam = new FakeSteamService(GuestId) { LobbyOwner = HostId, LobbyMembers = [HostId, GuestId] };
		var hostControl = new StubModsControl([Manifest(NetworkMode.RequiresAllPlayers)], complete: false);
		var guestControl = new StubModsControl([]);
		var guestProvider = new StubModListProvider([Info(NetworkMode.RequiresAllPlayers)]);
		var host = TestNode.Create(HostId, network, hostSteam, clock, pumpFirstFrame: true,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModsControl>(hostControl)));
		var guest = TestNode.Create(GuestId, network, guestSteam, clock, pumpFirstFrame: true,
			extraRegistrations: s =>
			{
				s.Replace(ServiceDescriptor.Singleton<IModsControl>(guestControl));
				s.Replace(ServiceDescriptor.Singleton<IModListProvider>(guestProvider));
			});
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		Assert.False(GuestHandshaken(host), "the pre-discovery handshake must be refused as pending");

		hostControl.Complete = true; // the first frame ran — discovery complete
		guest.Clock.Advance(1100); // past the 1 s retry interval
		guest.Update(); // the retry handshake re-runs the check

		Assert.True(GuestHandshaken(host), "the retry must pass once the discovery completed");
	}

	// ---- Stubs (nested — they belong to this test) ----

	private sealed class StubModsControl : IModsControl
	{
		private readonly List<ModManifest> _manifests;

		internal StubModsControl(List<ModManifest> manifests, bool complete = true)
		{
			_manifests = manifests;
			Complete = complete;
		}

		internal bool Complete { get; set; }

		public void FireModMessageReceived(ulong sender, ModMessageMsg msg)
		{
		}

		public void FireModCommandRequestReceived(ulong sender, ModCommandRequestMsg msg)
		{
		}

		public void FireModCommandResultReceived(ulong sender, ModCommandResultMsg msg)
		{
		}

		public IReadOnlyList<ModManifest> CurrentModManifests => _manifests;

		public bool IsDiscoveryComplete => Complete;
	}

	private sealed class StubModListProvider : IModListProvider
	{
		private readonly List<ModInfoMsg> _infos;

		internal StubModListProvider(List<ModInfoMsg> infos)
		{
			_infos = infos;
		}

		public List<ModInfoMsg> CurrentModInfos() => _infos;
	}
}
