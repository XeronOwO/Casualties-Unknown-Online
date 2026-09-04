using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The authority rule behind the guest world-item collision-sound fix.
/// A guest session may simulate world-item copies locally for smooth motion,
/// but those copies are not the physics authority; their native collision
/// presentation (drop/step sounds, dust, plush squeak) must only play on the
/// host/solo side where the simulation is authoritative.
/// </summary>
public class NonAuthoritativeItemImpactPolicyTests
{
	private static readonly Type Policy = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Items.NonAuthoritativeItemImpactPolicy",
		throwOnError: true)!;

	private static bool ShouldSuppress(bool isSessionActive, bool isHostMode, bool isStandaloneWorldItem) =>
		(bool)Policy.GetMethod("ShouldSuppress",
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
}
