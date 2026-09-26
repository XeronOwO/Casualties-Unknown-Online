using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The authority rule behind the guest world-item collision-sound fix.
/// A guest session may simulate world-item copies locally for smooth motion,
/// but those copies are not the physics authority; their native collision
/// presentation (drop/step sounds, dust, plush squeak) must only play on the
/// host/solo side where the simulation is authoritative. The same rule's other
/// half is what the AUTHORITY reports: the side that simulates the landing keeps
/// the presentation and sends it to the guests, so the two halves are exact
/// opposites for one collision and can never both fire.
/// </summary>
[Trait("Category", "Integration")]
public class NonAuthoritativeItemImpactPolicyTests
{
	private static readonly Type Policy = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Items.NonAuthoritativeItemImpactPolicy",
		throwOnError: true)!;

	private static bool ShouldSuppress(bool isSessionActive, bool isHostMode, bool isStandaloneWorldItem) =>
		(bool)Policy.GetMethod("ShouldSuppress",
			BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
			.Invoke(null, [isSessionActive, isHostMode, isStandaloneWorldItem])!;

	private static bool ShouldReport(bool isSessionActive, bool isHostMode, bool isStandaloneWorldItem) =>
		(bool)Policy.GetMethod("ShouldReport",
			BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
			.Invoke(null, [isSessionActive, isHostMode, isStandaloneWorldItem])!;

	[Theory]
	[InlineData(false, false, false, false)] // no session
	[InlineData(true, true, true, false)]     // host
	[InlineData(true, false, false, false)]   // guest, non-standalone
	[InlineData(true, false, true, true)]     // guest, standalone world item — suppress non-authoritative impact
	public void ShouldSuppress_OnlyForGuestStandaloneWorldCopies(
		bool isSessionActive, bool isHostMode, bool isStandaloneWorldItem, bool expected) =>
		Assert.Equal(expected, ShouldSuppress(isSessionActive, isHostMode, isStandaloneWorldItem));

	[Theory]
	[InlineData(false, false, false, false)] // no session
	[InlineData(true, true, true, true)]      // host, standalone world item — the authority reports the landing
	[InlineData(true, true, false, false)]    // host, carried / container item — no world collision presentation
	[InlineData(true, false, true, false)]    // guest, standalone world item — the suppressed half never reports
	[InlineData(true, false, false, false)]   // guest, non-standalone
	[InlineData(false, true, true, false)]    // host without a session (menu / solo teardown)
	public void ShouldReport_OnlyForHostStandaloneWorldItems(
		bool isSessionActive, bool isHostMode, bool isStandaloneWorldItem, bool expected) =>
		Assert.Equal(expected, ShouldReport(isSessionActive, isHostMode, isStandaloneWorldItem));

	[Theory]
	[InlineData(false, false, true)]
	[InlineData(true, true, true)]
	[InlineData(true, false, true)]
	public void TheTwoHalves_NeverBothFireForOneCollision(bool isSessionActive, bool isHostMode, bool isStandaloneWorldItem) =>
		Assert.False(
			ShouldReport(isSessionActive, isHostMode, isStandaloneWorldItem)
			&& ShouldSuppress(isSessionActive, isHostMode, isStandaloneWorldItem));
}
