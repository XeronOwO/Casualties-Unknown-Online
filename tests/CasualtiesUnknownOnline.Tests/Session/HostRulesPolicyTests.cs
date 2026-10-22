using System;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The minimal host-rules decision surface: the pure late-join/auto-continue
/// policy, the native-binding parity text mapping (the config entry and the
/// console JSON both write text) and the stateless composition service over
/// <see cref="HostRulesOptions"/> + <see cref="RespawnOptions"/>.
/// </summary>
public class HostRulesPolicyTests
{
	[Theory]
	[InlineData(true, true)]
	[InlineData(false, true)]
	public void CanAcceptNewMember_AllowLateJoinTrue_AlwaysAccepts(bool hostInWorld, bool expected) =>
		Assert.Equal(expected, HostRulesPolicy.CanAcceptNewMember(allowLateJoin: true, hostLocalInWorld: hostInWorld));

	[Theory]
	[InlineData(true, false)] // host in world + late join disabled -> rejected
	[InlineData(false, true)] // host in menu + late join disabled -> accepted
	public void CanAcceptNewMember_AllowLateJoinFalse_GatesOnlyRunningWorld(bool hostInWorld, bool expected) =>
		Assert.Equal(expected, HostRulesPolicy.CanAcceptNewMember(allowLateJoin: false, hostLocalInWorld: hostInWorld));

	[Fact]
	public void CanAutoContinue_ReflectsFlag()
	{
		Assert.True(HostRulesPolicy.CanAutoContinue(new HostRulesOptions { AutoContinue = true }));
		Assert.False(HostRulesPolicy.CanAutoContinue(new HostRulesOptions()));
	}

	// A host that updated first must not lock out members whose declaration is
	// merely unseen yet — warn is the conservative default.
	[Fact]
	public void DefaultNativeBindingParity_IsWarn() =>
		Assert.Equal(NativeBindingParity.Warn, new HostRulesOptions().NativeBindingParity);

	[Theory]
	[InlineData("allow", NativeBindingParity.Allow)]
	[InlineData("Warn", NativeBindingParity.Warn)]
	[InlineData("  require  ", NativeBindingParity.Require)]
	public void NativeBindingParityText_ParsesTheThreeLevelsAndRoundTrips(string text, NativeBindingParity expected)
	{
		Assert.True(NativeBindingParityText.TryParse(text, out var parity));
		Assert.Equal(expected, parity);
		Assert.True(NativeBindingParityText.TryParse(NativeBindingParityText.Format(expected), out var roundTripped));
		Assert.Equal(expected, roundTripped);
	}

	[Theory]
	[InlineData("")]
	[InlineData("parity")]
	[InlineData(null)]
	public void NativeBindingParityText_UnknownValue_FailsAndFallsBackToWarn(string? text)
	{
		Assert.False(NativeBindingParityText.TryParse(text, out var parity));
		Assert.Equal(NativeBindingParity.Warn, parity);
	}

	[Fact]
	public void DefaultRemoteBackpackAccess_IsAllow() =>
		Assert.Equal(RemoteBackpackAccess.Allow, new HostRulesOptions().RemoteBackpackAccess);

	[Theory]
	[InlineData("allow", RemoteBackpackAccess.Allow)]
	[InlineData("Unconscious_Or_Dead", RemoteBackpackAccess.UnconsciousOrDeadOnly)]
	[InlineData("  deny  ", RemoteBackpackAccess.Deny)]
	public void RemoteBackpackAccessText_ParsesTheThreeLevelsAndRoundTrips(string text, RemoteBackpackAccess expected)
	{
		Assert.True(RemoteBackpackAccessText.TryParse(text, out var access));
		Assert.Equal(expected, access);
		Assert.True(RemoteBackpackAccessText.TryParse(RemoteBackpackAccessText.Format(expected), out var roundTripped));
		Assert.Equal(expected, roundTripped);
	}

	[Theory]
	[InlineData("")]
	[InlineData("owner")]
	[InlineData(null)]
	public void RemoteBackpackAccessText_UnknownValue_FailsAndFallsBackToAllow(string? text)
	{
		Assert.False(RemoteBackpackAccessText.TryParse(text, out var access));
		Assert.Equal(RemoteBackpackAccess.Allow, access);
	}

	[Theory]
	[InlineData(RemoteBackpackAccess.Allow, false, false, false, true)] // allow needs no state at all
	[InlineData(RemoteBackpackAccess.Deny, true, true, true, false)]
	[InlineData(RemoteBackpackAccess.UnconsciousOrDeadOnly, true, true, true, false)] // conscious/alive
	[InlineData(RemoteBackpackAccess.UnconsciousOrDeadOnly, true, false, true, true)] // unconscious
	[InlineData(RemoteBackpackAccess.UnconsciousOrDeadOnly, true, false, false, true)] // dead
	[InlineData(RemoteBackpackAccess.UnconsciousOrDeadOnly, false, false, false, false)] // unknown fails closed
	[InlineData(RemoteBackpackAccess.Deny, false, false, false, false)]
	public void RemoteBackpackAccessPolicy_OperatesOnlyOnWhatTheRuleAllows(
		RemoteBackpackAccess access,
		bool healthKnown,
		bool conscious,
		bool alive,
		bool expected) =>
		Assert.Equal(expected, RemoteBackpackAccessPolicy.MayOperate(access, healthKnown, conscious, alive));

	[Theory]
	[InlineData(RemoteBackpackAccess.Allow, true)]
	[InlineData(RemoteBackpackAccess.UnconsciousOrDeadOnly, true)] // opens view only, but opens
	[InlineData(RemoteBackpackAccess.Deny, false)]
	public void RemoteBackpackAccessPolicy_OpensEverythingButDeny(RemoteBackpackAccess access, bool expected) =>
		Assert.Equal(expected, RemoteBackpackAccessPolicy.MayOpen(access));

	[Theory]
	[InlineData(RemoteBackpackAccess.Allow, false, true, true, false)]
	[InlineData(RemoteBackpackAccess.UnconsciousOrDeadOnly, true, true, true, true)]
	[InlineData(RemoteBackpackAccess.UnconsciousOrDeadOnly, true, false, true, false)]
	[InlineData(RemoteBackpackAccess.UnconsciousOrDeadOnly, false, false, false, true)]
	[InlineData(RemoteBackpackAccess.Deny, true, true, true, false)] // a closed view is not "view only"
	public void RemoteBackpackAccessPolicy_ViewOnlyIsAnOpenViewThatMayNotOperate(
		RemoteBackpackAccess access,
		bool healthKnown,
		bool conscious,
		bool alive,
		bool expected) =>
		Assert.Equal(expected, RemoteBackpackAccessPolicy.IsViewOnly(access, healthKnown, conscious, alive));

	[Fact]
	public void HostRulesService_ComposesNewFlagsAndRespawnFlags()
	{
		var service = new HostRulesService(
			new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions
			{
				PvpEnabled = true,
				AutoContinue = true,
				AllowLateJoin = false,
				RemoteBackpackAccess = RemoteBackpackAccess.Deny,
				WidenRunSettings = true,
				PiggybackWeightMultiplier = 1.25f,
				NativeBindingParity = NativeBindingParity.Require,
			}),
			new MutableOptionsMonitor<RespawnOptions>(new RespawnOptions
			{
				Permadeath = true,
				ReviveFromTrader = false,
				ReviveOnNextLevel = false,
				RespawnKeepInventory = false,
				RespawnKeepSkills = false,
			}),
			new AnnouncedHostRules(new FakeSessionControl(), NullLogger<AnnouncedHostRules>.Instance));

		Assert.True(service.PvpEnabled);
		Assert.True(service.AutoContinue);
		Assert.False(service.AllowLateJoin);
		Assert.Equal(RemoteBackpackAccess.Deny, service.RemoteBackpackAccess);
		Assert.True(service.WidenRunSettings);
		Assert.True(Math.Abs(service.PiggybackWeightMultiplier - 1.25f) < 0.001f);
		Assert.Equal(NativeBindingParity.Require, service.NativeBindingParity);
		Assert.True(service.Permadeath);
		Assert.False(service.SaveInventory);
		Assert.False(service.ReviveFromTrader);
		Assert.False(service.ReviveOnNextLevel);
	}

	[Fact]
	public void HostRulesService_HotReloadReflectsCurrentOptions()
	{
		var hostRules = new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions { AllowLateJoin = true });
		var service = new HostRulesService(hostRules,
			new MutableOptionsMonitor<RespawnOptions>(new RespawnOptions()),
			new AnnouncedHostRules(new FakeSessionControl(), NullLogger<AnnouncedHostRules>.Instance));
		Assert.True(service.AllowLateJoin);
		Assert.Equal(RemoteBackpackAccess.Allow, service.RemoteBackpackAccess);

		hostRules.Set(new HostRulesOptions { AllowLateJoin = false, RemoteBackpackAccess = RemoteBackpackAccess.UnconsciousOrDeadOnly, PvpEnabled = true });

		Assert.False(service.AllowLateJoin);
		Assert.Equal(RemoteBackpackAccess.UnconsciousOrDeadOnly, service.RemoteBackpackAccess);
		Assert.True(service.PvpEnabled);
	}
}
