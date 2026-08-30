using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Locks the pure host-side enemy combat order policy extracted from
/// <c>EnemyCombatDirector</c>: the director may only turn a selected victim
/// into a remote order, a native path, or a host item fallback through this
/// Runtime decision surface. This makes the ordering rule testable without
/// Unity and prepares the same rule for a future kernel process.
/// </summary>
public class EnemyCombatOrderPolicyTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;

	private static EnemyTargetFact Target(ulong steamId, float x, float y) => new(steamId, new NetVector2(x, y));

	[Fact]
	public void SpiderBite_Null_IsNone() =>
		Assert.Equal(EnemyCombatOrderPolicy.ApplyPath.None, EnemyCombatOrderPolicy.DecideSpiderBite(null, HostId));

	[Fact]
	public void SpiderBite_RemoteVictim_IsRemoteOrder() =>
		Assert.Equal(
			EnemyCombatOrderPolicy.ApplyPath.RemoteOrder,
			EnemyCombatOrderPolicy.DecideSpiderBite(Target(GuestId, 1f, 0f), HostId));

	[Fact]
	public void SpiderBite_LocalVictim_IsLocalNative() =>
		Assert.Equal(
			EnemyCombatOrderPolicy.ApplyPath.LocalNative,
			EnemyCombatOrderPolicy.DecideSpiderBite(Target(HostId, 0f, 0f), HostId));

	[Fact]
	public void CrystalLunge_Null_IsNone() =>
		Assert.Equal(EnemyCombatOrderPolicy.ApplyPath.None, EnemyCombatOrderPolicy.DecideCrystalLunge(null, HostId));

	[Fact]
	public void CrystalLunge_RemoteVictim_IsRemoteOrder() =>
		Assert.Equal(
			EnemyCombatOrderPolicy.ApplyPath.RemoteOrder,
			EnemyCombatOrderPolicy.DecideCrystalLunge(Target(GuestId, 3f, 0f), HostId));

	[Fact]
	public void CrystalLunge_LocalVictim_IsLocalNative() =>
		Assert.Equal(
			EnemyCombatOrderPolicy.ApplyPath.LocalNative,
			EnemyCombatOrderPolicy.DecideCrystalLunge(Target(HostId, 3f, 0f), HostId));

	[Fact]
	public void ItemHit_NativeHandled_IsLocalNative() =>
		Assert.Equal(EnemyCombatOrderPolicy.ApplyPath.LocalNative, EnemyCombatOrderPolicy.DecideItemHit(true, true));

	[Fact]
	public void ItemHit_NativeNotHandled_WithPlayerNear_IsHostItemFallback() =>
		Assert.Equal(EnemyCombatOrderPolicy.ApplyPath.HostItemFallback, EnemyCombatOrderPolicy.DecideItemHit(false, true));

	[Fact]
	public void ItemHit_NativeNotHandled_NoPlayerNear_IsNone() =>
		Assert.Equal(EnemyCombatOrderPolicy.ApplyPath.None, EnemyCombatOrderPolicy.DecideItemHit(false, false));
}
