using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The minimal host-rules decision surface: the pure late-join/auto-continue
/// policy and the stateless composition service over
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

	[Fact]
	public void HostRulesService_ComposesNewFlagsAndRespawnFlags()
	{
		var service = new HostRulesService(
			new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions
			{
				PvpEnabled = true,
				AutoContinue = true,
				AllowLateJoin = false,
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

		hostRules.Set(new HostRulesOptions { AllowLateJoin = false, PvpEnabled = true });

		Assert.False(service.AllowLateJoin);
		Assert.True(service.PvpEnabled);
	}
}
