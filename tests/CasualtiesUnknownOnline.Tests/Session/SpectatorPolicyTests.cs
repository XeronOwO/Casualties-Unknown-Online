using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The one rule both readers of "who may watch" call: the camera, which enforces it, and the member card, which
/// decides whether to offer the button. It is pinned here because the two copies that used to exist drifted
/// silently — the card stopped reading the host's unconscious switch and nothing failed.
/// </summary>
public class SpectatorPolicyTests
{
	[Fact]
	public void ADeadCharacterMayWatch() =>
		Assert.True(SpectatorPolicy.MayWatch(
			localAlive: false,
			localConscious: false,
			spectateWhileUnconscious: false,
			sessionActive: true));

	[Fact]
	public void ALivingCharacterMayNot() =>
		Assert.False(SpectatorPolicy.MayWatch(
			localAlive: true,
			localConscious: true,
			spectateWhileUnconscious: true,
			sessionActive: true));

	[Fact]
	public void AnUnconsciousCharacterMayOnlyWhenTheHostAllowsIt()
	{
		// The host's own switch, read by both readers: an unconscious body is still its owner's to act on, so
		// letting it watch changes a live player's client rather than a dead one's.
		Assert.False(SpectatorPolicy.MayWatch(
			localAlive: true,
			localConscious: false,
			spectateWhileUnconscious: false,
			sessionActive: true));

		Assert.True(SpectatorPolicy.MayWatch(
			localAlive: true,
			localConscious: false,
			spectateWhileUnconscious: true,
			sessionActive: true));
	}

	[Fact]
	public void WithoutASessionNobodyWatches() =>
		Assert.False(SpectatorPolicy.MayWatch(
			localAlive: false,
			localConscious: false,
			spectateWhileUnconscious: true,
			sessionActive: false));
}
