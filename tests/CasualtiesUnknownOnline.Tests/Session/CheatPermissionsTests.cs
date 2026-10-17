using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Permissions;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The host's cheat-permission roster in isolation: who it holds, the standings it
/// refuses to hold, and the session boundary that drops it. The replicated half (the
/// announcement reaching a member) and the two-sided verdict are pinned over the real
/// stack by <see cref="CheatPermissionReplicationTests"/> and the console suites.
///
/// <para>
/// The IP-direct refusal is the ticket's identity decision, so it is pinned here as a
/// VALUE: the transport cannot prove an account in that mode, and this roster answers
/// "not granted" rather than keying a grant on a connection's own logical id.
/// </para>
/// </summary>
public class CheatPermissionsTests
{
	private const ulong Host = 1001;
	private const ulong Guest = 2001;
	private const ulong Other = 3001;

	[Fact]
	public void AFreshSession_GrantsNobody()
	{
		var roster = Roster(out _);

		Assert.False(roster.IsGranted(Guest));
		Assert.Empty(roster.Granted);
	}

	[Fact]
	public void Grant_AddsTheMember_AndRevokeTakesItBack()
	{
		var roster = Roster(out var session);
		session.AddMember(Guest, "bob");

		Assert.True(roster.Grant(Guest, out var refusal), refusal);
		Assert.True(roster.IsGranted(Guest));
		Assert.Equal([Guest], roster.Granted);

		Assert.True(roster.Revoke(Guest, out refusal), refusal);
		Assert.False(roster.IsGranted(Guest));
		Assert.Empty(roster.Granted);
	}

	[Fact]
	public void TheRoster_IsSorted_SoTheAnnouncementDoesNotMoveWhenOnlyTheOrderDid()
	{
		var roster = Roster(out var session);
		session.AddMember(Guest, "bob");
		session.AddMember(Other, "carol");

		roster.Grant(Other, out _);
		roster.Grant(Guest, out _);

		Assert.Equal([Guest, Other], roster.Granted);
	}

	[Fact]
	public void TheHostItself_IsRefusedByName_BecauseItsStandingIsTheOnlineSwitch()
	{
		var roster = Roster(out _);

		Assert.False(roster.Grant(Host, out var refusal));
		Assert.Contains("online switch", refusal);
		Assert.False(roster.IsGranted(Host));
	}

	[Fact]
	public void APeerThatIsNotAHandshakenMember_IsRefused()
	{
		var roster = Roster(out var session);
		session.AddMember(Guest, "bob", handshaken: false);

		Assert.False(roster.Grant(Guest, out var refusal));
		Assert.Contains("not a handshaken member", refusal);

		Assert.False(roster.Grant(9999, out refusal));
		Assert.Contains("not a handshaken member", refusal);
	}

	[Fact]
	public void AnIpDirectSession_GrantsNobody_AndSaysWhy()
	{
		var roster = Roster(out var session, ipDirect: true);
		session.AddMember(Guest, "bob");

		Assert.False(roster.Grant(Guest, out var refusal));
		Assert.Contains("IP-direct", refusal);
		Assert.False(roster.IsGranted(Guest));
	}

	[Fact]
	public void NoActiveSession_EditsNothing()
	{
		var roster = Roster(out var session);
		session.AddMember(Guest, "bob");
		session.SessionActive = false;

		Assert.False(roster.Grant(Guest, out var refusal));
		Assert.Contains("while its session runs", refusal);
	}

	[Fact]
	public void AHostThatBecameAMember_EditsNothing()
	{
		var roster = Roster(out var session);
		session.AddMember(Guest, "bob");
		Assert.True(roster.Grant(Guest, out _));

		// A grant an old host made survives on that object only until its own session end;
		// what matters here is that a process which is not the host cannot edit a roster.
		session.Role = SessionRole.Guest;
		Assert.False(roster.Grant(Guest, out var refusal));
		Assert.Contains("Only the host", refusal);
	}

	[Fact]
	public void TheSessionEnding_DropsTheRoster()
	{
		var roster = Roster(out var session);
		session.AddMember(Guest, "bob");
		Assert.True(roster.Grant(Guest, out _));

		session.FireSessionEnded();

		Assert.Empty(roster.Granted);
		Assert.False(roster.IsGranted(Guest), "a grant belongs to the session that made it");
	}

	[Fact]
	public void AGrantAlreadyHeld_IsRefusedByName()
	{
		var roster = Roster(out var session);
		session.AddMember(Guest, "bob");
		Assert.True(roster.Grant(Guest, out _));

		Assert.False(roster.Grant(Guest, out var refusal));
		Assert.Contains("already holds", refusal);
	}

	[Fact]
	public void ARevokeWithNoGrant_IsRefusedByName()
	{
		var roster = Roster(out var session);
		session.AddMember(Guest, "bob");

		Assert.False(roster.Revoke(Guest, out var refusal));
		Assert.Contains("does not hold", refusal);
	}

	private static CheatPermissions Roster(out FakeSessionControl session, bool ipDirect = false)
	{
		session = new FakeSessionControl { Role = SessionRole.Host, LocalSteamId = Host, HostSteamId = Host, SessionActive = true };
		return new CheatPermissions(
			session,
			new FakeTransportIdentity { IsIpDirect = ipDirect, LocalPeerId = Host },
			NullLogger<CheatPermissions>.Instance);
	}
}
