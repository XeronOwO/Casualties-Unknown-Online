using System;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The two halves of the announcement below the wire: the member-side store the
/// receive handler writes and the local check reads
/// (<see cref="AnnouncedHostRules"/>), and the surface in force for this process that
/// overrides a member's own config with what the host announced
/// (<see cref="HostRulesService"/>).
///
/// <para>
/// The case that matters most is the one with NOTHING announced: it must read as "the
/// local check is skipped", never as "everything is allowed" and never as a guess — a
/// member whose belief is stale, lost or absent has to fall back to the behaviour that
/// existed before the rules were replicated, where the host's own verdict is the only
/// one. The other is the session BOUNDARY: the set belongs to the session that
/// announced it, so a process that was a member and later hosts (or joins a different
/// host) must not keep enforcing the previous host's rules.
/// </para>
/// </summary>
public class HostRuleAnnouncementTests
{
	[Fact]
	public void NothingAnnounced_ReadsAsNoSetAndNoLocalRefusal()
	{
		var rules = New();

		Assert.Null(rules.Current);
		Assert.False(rules.Forbids(HostRuleId.AllowMemberItemDataWrite));
	}

	[Fact]
	public void AnAnnouncedRuleThatIsOff_ForbidsTheGesture()
	{
		var rules = New();
		rules.Apply(Set(allowMemberItemDataWrite: false));

		Assert.True(rules.Forbids(HostRuleId.AllowMemberItemDataWrite));
	}

	[Fact]
	public void AnAnnouncedRuleThatIsOn_DoesNotForbidIt()
	{
		var rules = New();
		rules.Apply(Set(allowMemberItemDataWrite: true));

		Assert.False(rules.Forbids(HostRuleId.AllowMemberItemDataWrite));
	}

	[Fact]
	public void ARepeatAnnouncement_IsAbsorbedAndSaysSo()
	{
		var rules = New();
		var set = Set(allowMemberItemDataWrite: false);

		Assert.True(rules.Apply(set), "the first set is a change");
		Assert.False(rules.Apply(set), "the world-entry and repair groups re-send the same set on purpose");
		Assert.True(rules.Forbids(HostRuleId.AllowMemberItemDataWrite));
	}

	[Fact]
	public void ANewerAnnouncement_ReplacesTheWholeSet()
	{
		var rules = New();
		rules.Apply(Set(allowMemberItemDataWrite: false));
		rules.Apply(Set(allowMemberItemDataWrite: true));

		Assert.False(rules.Forbids(HostRuleId.AllowMemberItemDataWrite));
	}

	[Fact]
	public void ANumberRule_IsNotReadAsAGesture()
	{
		var rules = New();
		rules.Apply(Set(allowMemberItemDataWrite: false));

		Assert.Throws<ArgumentOutOfRangeException>(() => rules.Forbids(HostRuleId.PiggybackWeightMultiplier));
	}

	/// <summary>
	/// The session boundary: the announced set belongs to the session that announced it.
	/// A process that was a member and later hosts would otherwise enforce the previous
	/// host's rules as its own — its own members would be admitted, refused or judged
	/// against rules nobody chose for it — so the store drops the set on the session-end
	/// edge and the fallback (own config, local check skipped) takes over.
	/// </summary>
	[Fact]
	public void TheSessionEnd_DropsTheAnnouncedSet()
	{
		var session = new FakeSessionControl { Role = SessionRole.Guest };
		var rules = new AnnouncedHostRules(session, NullLogger<AnnouncedHostRules>.Instance);
		rules.Apply(Set(allowMemberItemDataWrite: false));
		Assert.True(rules.Forbids(HostRuleId.AllowMemberItemDataWrite));

		session.FireSessionEnded();

		Assert.Null(rules.Current);
		Assert.False(rules.Forbids(HostRuleId.AllowMemberItemDataWrite));
	}

	[Fact]
	public void TheSessionEnd_OfAProcessThatNeverReceivedASet_ChangesNothing()
	{
		var session = new FakeSessionControl { Role = SessionRole.Guest };
		var rules = new AnnouncedHostRules(session, NullLogger<AnnouncedHostRules>.Instance);

		session.FireSessionEnded();

		Assert.Null(rules.Current);
	}

	[Fact]
	public void WithNothingAnnounced_TheMembersOwnConfigIsWhatItReads()
	{
		var service = Service(new HostRulesOptions { AllowMemberItemDataWrite = true, PiggybackWeightMultiplier = 1.25f }, new RespawnOptions { Permadeath = true });

		Assert.True(service.AllowMemberItemDataWrite);
		Assert.Equal(1.25f, service.PiggybackWeightMultiplier);
		Assert.True(service.Permadeath);
	}

	[Fact]
	public void WithAnAnnouncement_TheHostsValuesWinOnBothHalvesOfTheSurface()
	{
		var announced = New();
		var service = Service(
			new HostRulesOptions { AllowMemberItemDataWrite = true, PiggybackWeightMultiplier = 1.25f },
			new RespawnOptions { Permadeath = false },
			announced);

		announced.Apply(new HostRuleSet(
			PvpEnabled: false,
			AutoContinue: false,
			AllowLateJoin: false,
			AllowRemoteInventoryTake: false,
			AllowMemberItemDataWrite: false,
			WidenRunSettings: false,
			PiggybackWeightMultiplier: 0.25f,
			NativeBindingParity: NativeBindingParity.Require,
			SaveInventory: false,
			ReviveFromTrader: false,
			ReviveOnNextLevel: false,
			Permadeath: true,
			RadiationLineStopsAboveTheFloor: false,
			SpectatorFreeFly: false,
			SpectateWhileUnconscious: true,
			SpectateWhileWaitingToJoin: true));

		Assert.False(service.AllowMemberItemDataWrite);
		Assert.Equal(0.25f, service.PiggybackWeightMultiplier);
		Assert.True(service.Permadeath);
		Assert.Equal(NativeBindingParity.Require, service.NativeBindingParity);
		Assert.Equal(announced.Current!.Value, service.Current);
	}

	[Fact]
	public void TheRespawnSection_KeepsItsOwnDefaultWhenNothingIsAnnounced()
	{
		// The one name the two sections disagree on: the rule surface calls it
		// SaveInventory, the config section calls it RespawnKeepInventory. A mapping
		// that read the wrong one would answer every other rule correctly.
		var service = Service(new HostRulesOptions(), new RespawnOptions { RespawnKeepInventory = false });

		Assert.False(service.SaveInventory);
	}

	/// <summary>The store over a session the test drives — the fake's own SessionEnded edge is what the reset cases fire.</summary>
	private static AnnouncedHostRules New() => new(new FakeSessionControl(), NullLogger<AnnouncedHostRules>.Instance);

	private static HostRuleSet Set(bool allowMemberItemDataWrite) => new(
		PvpEnabled: false,
		AutoContinue: false,
		AllowLateJoin: true,
		AllowRemoteInventoryTake: true,
		AllowMemberItemDataWrite: allowMemberItemDataWrite,
		WidenRunSettings: true,
		PiggybackWeightMultiplier: 0.8f,
		NativeBindingParity: NativeBindingParity.Warn,
		SaveInventory: true,
		ReviveFromTrader: true,
		ReviveOnNextLevel: true,
		Permadeath: false,
		RadiationLineStopsAboveTheFloor: false,
		SpectatorFreeFly: false,
		SpectateWhileUnconscious: true,
		SpectateWhileWaitingToJoin: true);

	private static HostRulesService Service(HostRulesOptions hostRules, RespawnOptions respawn, AnnouncedHostRules? announced = null) =>
		new(new MutableOptionsMonitor<HostRulesOptions>(hostRules), new MutableOptionsMonitor<RespawnOptions>(respawn), announced ?? New());
}
