using System;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
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
	public void HostRulesService_ComposesNewFlagsAndRespawnFlags()
	{
		var service = new HostRulesService(
			new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions
			{
				PvpEnabled = true,
				AutoContinue = true,
				AllowLateJoin = false,
				AllowRemoteInventoryTake = false,
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
			}));

		Assert.True(service.PvpEnabled);
		Assert.True(service.AutoContinue);
		Assert.False(service.AllowLateJoin);
		Assert.False(service.AllowRemoteInventoryTake);
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
			new MutableOptionsMonitor<RespawnOptions>(new RespawnOptions()));
		Assert.True(service.AllowLateJoin);
		Assert.True(service.AllowRemoteInventoryTake);

		hostRules.Set(new HostRulesOptions { AllowLateJoin = false, AllowRemoteInventoryTake = false, PvpEnabled = true });

		Assert.False(service.AllowLateJoin);
		Assert.False(service.AllowRemoteInventoryTake);
		Assert.True(service.PvpEnabled);
	}
}
