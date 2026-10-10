using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
/// refused as "pending" and passes on the retry. The declared native binding is
/// judged separately by the host's parity policy over the mods BOTH sides list:
/// allow admits silently, warn (the default) admits and records the mismatch,
/// require refuses and names the mod and both declarations — an undeclared or
/// blank declaration is a difference, and a mod only one side lists is not judged.
/// </summary>
[Trait("Category", "Integration")]
public class ModHandshakeTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	// The matrix's one mod id — declared per test with the mode/version needed.
	private const string ModId = "test.matrix";

	private static ModManifest Manifest(NetworkMode mode, string version = "1.0.0", ModPermission permissions = ModPermission.None, string? binding = null) =>
		new(ModId, "Matrix Mod", version, mode, null, permissions, nativeBinding: binding);

	private static ModInfoMsg Info(NetworkMode mode, string version = "1.0.0", ModPermission permissions = ModPermission.None, string? binding = null, string? content = null) =>
		new() { Id = ModId, Version = version, NetworkMode = mode, Permissions = permissions, NativeBinding = binding, ContentFingerprint = content };

	/// <summary>A mod the host does not list at all — the parity rule has no counterpart to compare.</summary>
	private static ModInfoMsg InfoFor(string id, NetworkMode mode, string? binding = null) =>
		new() { Id = id, Version = "1.0.0", NetworkMode = mode, NativeBinding = binding };

	private static (TestNode Host, TestNode Guest) CreatePair(
		List<ModManifest> hostMods, List<ModInfoMsg> guestInfos, bool hostComplete = true,
		NativeBindingParity parity = NativeBindingParity.Warn, RecordingLoggerFactory? recorder = null,
		Action<TestNode>? arrangeHost = null)
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
				s.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(
					new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions { NativeBindingParity = parity })));
				if (recorder is not null)
				{
					s.Replace(ServiceDescriptor.Singleton<ILoggerFactory>(recorder));
				}
			});
		var guest = TestNode.Create(GuestId, network, guestSteam, clock, pumpFirstFrame: true,
			extraRegistrations: s =>
			{
				s.Replace(ServiceDescriptor.Singleton<IModsControl>(guestControl));
				s.Replace(ServiceDescriptor.Singleton<IModListProvider>(guestProvider));
			});
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		// The host's own content has to be in place BEFORE the member's join is judged: the
		// handshake compares two fingerprints, and one of them is the host's.
		arrangeHost?.Invoke(host);
		guest.Steam.FireLobbyEntered(LobbyId);
		return (host, guest);
	}

	/// <summary>Files one item definition into the host's own content registry, under the matrix mod's id.</summary>
	private static void RegisterHostContent(TestNode host, string itemId) =>
		host.Services.GetRequiredService<ModContentStore>().Add(ModId, null, new ModItemDefinition { Id = itemId });

	/// <summary>The digest the host computes for a content set of its own — the guest side of an equal pair.</summary>
	private static string HostDigestOf(string itemId)
	{
		var store = new ModContentStore();
		store.Add(ModId, null, new ModItemDefinition { Id = itemId });
		return store.ByMod[ModId];
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

	// ---- Native-binding parity (the declared fact, judged apart from the contract) ----

	[Fact]
	public void EqualNativeBindings_Accepted()
	{
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.RequiresAllPlayers, binding: "Game.Code.Foo")],
			[Info(NetworkMode.RequiresAllPlayers, binding: "Game.Code.Foo")],
			parity: NativeBindingParity.Require);

		Assert.True(GuestHandshaken(host), "equal declarations satisfy even a require policy");
	}

	[Fact]
	public void NoDeclarationOnEitherSide_Accepted()
	{
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.RequiresAllPlayers)],
			[Info(NetworkMode.RequiresAllPlayers)],
			parity: NativeBindingParity.Require);

		Assert.True(GuestHandshaken(host), "no declaration on either side is parity, not a difference");
	}

	[Fact]
	public void DifferingDeclarations_WarnPolicy_AdmitsAndRecordsTheMismatch()
	{
		var recorder = new RecordingLoggerFactory();
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.RequiresAllPlayers, binding: "Game.Code.Foo")],
			[Info(NetworkMode.RequiresAllPlayers, binding: "Game.Code.Bar")],
			recorder: recorder);

		Assert.True(GuestHandshaken(host), "the default warn policy admits the member");
		Assert.Contains(
			recorder.Messages(LogLevel.Warning, "HandshakeHandler"),
			message => message.Contains(ModId, StringComparison.Ordinal)
				&& message.Contains("Game.Code.Bar", StringComparison.Ordinal)
				&& message.Contains("Game.Code.Foo", StringComparison.Ordinal)
				&& message.Contains("warn parity policy", StringComparison.Ordinal));
	}

	[Fact]
	public void DifferingDeclarations_RequirePolicy_RefusesAndNamesTheModAndBothDeclarations()
	{
		var recorder = new RecordingLoggerFactory();
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.RequiresAllPlayers, binding: "Game.Code.Foo")],
			[Info(NetworkMode.RequiresAllPlayers, binding: "Game.Code.Bar")],
			parity: NativeBindingParity.Require, recorder: recorder);

		Assert.False(GuestHandshaken(host), "require refuses the member before it is created");
		Assert.Empty(host.Session.Members);
		Assert.Contains(
			recorder.Messages(LogLevel.Warning, "HandshakeHandler"),
			message => message.Contains(ModId, StringComparison.Ordinal)
				&& message.Contains("Game.Code.Bar", StringComparison.Ordinal)
				&& message.Contains("Game.Code.Foo", StringComparison.Ordinal)
				&& message.Contains("requires native-binding parity", StringComparison.Ordinal));
	}

	[Fact]
	public void DifferingDeclarations_AllowPolicy_AdmitsWithoutRecording()
	{
		var recorder = new RecordingLoggerFactory();
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.RequiresAllPlayers, binding: "Game.Code.Foo")],
			[Info(NetworkMode.RequiresAllPlayers, binding: "Game.Code.Bar")],
			parity: NativeBindingParity.Allow, recorder: recorder);

		Assert.True(GuestHandshaken(host));
		// Every level, not only Warning: "allow is silent" is the documented
		// contract, so a regression that recorded the mismatch at another level
		// must fail here too. The filter is proven reachable by the warn/require
		// cases above, which find their own lines through the same recorder.
		Assert.DoesNotContain(
			recorder.Entries.Where(entry => entry.Category.Contains("HandshakeHandler", StringComparison.Ordinal)),
			entry => entry.Message.Contains(ModId, StringComparison.Ordinal));
	}

	[Fact]
	public void UndeclaredGuestBinding_RequirePolicy_Refused()
	{
		var recorder = new RecordingLoggerFactory();
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.RequiresAllPlayers, binding: "Game.Code.Foo")],
			[Info(NetworkMode.RequiresAllPlayers)],
			parity: NativeBindingParity.Require, recorder: recorder);

		Assert.False(GuestHandshaken(host), "an undeclared binding is a difference, not an abstention");
		Assert.Contains(
			recorder.Messages(LogLevel.Warning, "HandshakeHandler"),
			message => message.Contains(ModId, StringComparison.Ordinal)
				&& message.Contains("native binding none", StringComparison.Ordinal));
	}

	[Fact]
	public void UndeclaredHostBinding_WarnPolicy_RecordsTheHostAsNone()
	{
		// The mirror image: the host declared none, the member declared one. Both
		// sides render the undeclared state as "none", so a log reader can tell it
		// apart from an empty or missing name.
		var recorder = new RecordingLoggerFactory();
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.RequiresAllPlayers)],
			[Info(NetworkMode.RequiresAllPlayers, binding: "Game.Code.Bar")],
			recorder: recorder);

		Assert.True(GuestHandshaken(host));
		Assert.Contains(
			recorder.Messages(LogLevel.Warning, "HandshakeHandler"),
			message => message.Contains(ModId, StringComparison.Ordinal)
				&& message.Contains("the host declares none", StringComparison.Ordinal));
	}

	[Theory]
	[InlineData("  Game.Code.Foo  ", "Game.Code.Foo", true)]
	[InlineData("game.code.foo", "Game.Code.Foo", false)]
	public void BindingComparison_IsTrimmedAndCaseSensitive(string hostBinding, string guestBinding, bool admitted)
	{
		// The comparison is exact after trimming (ordinal, case-sensitive), which
		// "Handshake consistency" in docs/en/reference/mod-api.md states: two spellings of the same name match, two
		// casings do not — a third-party author must spell the binding identically.
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.RequiresAllPlayers, binding: hostBinding)],
			[Info(NetworkMode.RequiresAllPlayers, binding: guestBinding)],
			parity: NativeBindingParity.Require);

		Assert.Equal(admitted, GuestHandshaken(host));
	}

	[Fact]
	public void BlankGuestBinding_NormalizesToUndeclared_RequirePolicyAccepted()
	{
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.RequiresAllPlayers)],
			[Info(NetworkMode.RequiresAllPlayers, binding: "   ")],
			parity: NativeBindingParity.Require);

		Assert.True(GuestHandshaken(host), "a blank declaration means 'none', exactly as discovery normalizes it");
	}

	[Fact]
	public void ModOnlyTheGuestLists_IsNotJudgedByParity()
	{
		var (host, _) = CreatePair(
			[],
			[InfoFor("test.guestonly", NetworkMode.ClientOnly, binding: "Game.Code.Other")],
			parity: NativeBindingParity.Require);

		Assert.True(GuestHandshaken(host), "parity compares the declarations of one mod id; a mod the host does not list has no counterpart");
	}

	// ---- What a declaration PRODUCED (the content fingerprint) ----

	[Fact]
	public void StateBearingModWithDifferentContent_Admitted_AndTheLogNamesTheModAndBothFingerprints()
	{
		// Id, version, mode and permissions can all agree while the two copies materialize
		// different content — a definition may compute its members (decision 251). The
		// difference is REPORTED, never a refusal (user ruling: the comparison covers
		// addresses, so a benign local difference is indistinguishable from a harmful one and
		// must not gate entry). The line names the mod and BOTH fingerprints, which is what
		// makes the divergence diagnosable afterwards.
		var recorder = new RecordingLoggerFactory();
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.RequiresAllPlayers)],
			[Info(NetworkMode.RequiresAllPlayers, content: "member-content")],
			recorder: recorder,
			arrangeHost: node => RegisterHostContent(node, "host.item"));

		Assert.True(GuestHandshaken(host), "a content difference is reported, never a refusal");
		var named = Assert.Single(recorder.Messages(LogLevel.Warning, "HandshakeHandler"));
		Assert.Contains(ModId, named, StringComparison.Ordinal);
		Assert.Contains("different content", named, StringComparison.Ordinal);
		Assert.Contains("member-content", named, StringComparison.Ordinal);
		Assert.Contains(HostDigestOf("host.item"), named, StringComparison.Ordinal);
	}

	[Fact]
	public void StateBearingModWithEqualContent_Accepted_AndNothingIsRecorded()
	{
		var recorder = new RecordingLoggerFactory();
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.RequiresAllPlayers)],
			[Info(NetworkMode.RequiresAllPlayers, content: HostDigestOf("shared.item"))],
			recorder: recorder,
			arrangeHost: node => RegisterHostContent(node, "shared.item"));

		Assert.True(GuestHandshaken(host));
		Assert.Empty(recorder.Messages(LogLevel.Warning, "HandshakeHandler"));
	}

	[Fact]
	public void StateBearingModWhoseContentOnlyTheMemberHas_IsReportedAndAdmitted()
	{
		// "No content" on one side against content on the other is a difference like any
		// other — not an unknown value to skip — and it takes the same verdict.
		var recorder = new RecordingLoggerFactory();
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.RequiresAllPlayers)],
			[Info(NetworkMode.RequiresAllPlayers, content: "member-only-content")],
			recorder: recorder);

		Assert.True(GuestHandshaken(host));
		Assert.Contains(recorder.Messages(LogLevel.Warning, "HandshakeHandler"), line =>
			line.Contains(ModId, StringComparison.Ordinal)
			&& line.Contains("none", StringComparison.Ordinal));
	}

	[Fact]
	public void LocalSurfaceModWithDifferentContent_TakesTheSameVerdict()
	{
		// The mode does not decide this row: the comparison can be wrong for a benign reason
		// under any mode, so every difference is reported and every member is admitted.
		var recorder = new RecordingLoggerFactory();
		var (host, _) = CreatePair(
			[Manifest(NetworkMode.ClientOnly)],
			[Info(NetworkMode.ClientOnly, content: "member-content")],
			recorder: recorder,
			arrangeHost: node => RegisterHostContent(node, "host.item"));

		Assert.True(GuestHandshaken(host));
		Assert.Contains(recorder.Messages(LogLevel.Warning, "HandshakeHandler"), line =>
			line.Contains(ModId, StringComparison.Ordinal)
			&& line.Contains("ClientOnly", StringComparison.Ordinal));
	}

	[Fact]
	public void ModWithNoContentOnEitherSide_IsNotReported()
	{
		// Both sides report nothing for the mod: two equal "no content" values, never a
		// skipped comparison — and nothing to report.
		var recorder = new RecordingLoggerFactory();
		var (host, _) = CreatePair([Manifest(NetworkMode.RequiresAllPlayers)], [Info(NetworkMode.RequiresAllPlayers)], recorder: recorder);

		Assert.True(GuestHandshaken(host));
		Assert.Empty(recorder.Messages(LogLevel.Warning, "HandshakeHandler"));
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
