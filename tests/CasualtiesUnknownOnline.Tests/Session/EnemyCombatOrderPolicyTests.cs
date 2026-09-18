using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Locks the pure host-side enemy combat apply-path policy. Since the attack
/// paths themselves became announcements judged by the client the effect lands on
/// (the 2026-09-18 ruling), what remains host-side is the item-vs-enemy fallback:
/// the director observes whether the game's native branch ran and may only reach
/// this Runtime decision surface, so the rule stays testable without Unity.
/// </summary>
public class EnemyCombatOrderPolicyTests
{
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
