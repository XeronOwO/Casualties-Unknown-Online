using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session.World;

/// <summary>
/// The wait's rule without a clock: a set that is still growing is held, a set that has
/// stood still is released, and a set that never stops growing is released at the ceiling
/// so its refused rows are reported.
/// </summary>
public sealed class GeyserFillWaitTests
{
	// The 2026-11-17 reading: 94 of 114 objects five seconds past the seam. A fixed
	// window released at five seconds and refused the rows the world was about to have.
	[Fact]
	public void Holds_WhileTheLiveSetIsStillGrowing() =>
		Assert.True(GeyserFillWait.Holds(live: 94, saved: 114, stableSeconds: 0.5, elapsedSeconds: 5));

	[Fact]
	public void Releases_OnceTheCountHasStoodStill() =>
		Assert.False(GeyserFillWait.Holds(live: 94, saved: 114, stableSeconds: 3.5, elapsedSeconds: 8));

	[Fact]
	public void Releases_AtTheCeilingWhileTheSetStillGrows() =>
		Assert.False(GeyserFillWait.Holds(live: 94, saved: 114, stableSeconds: 0.1, elapsedSeconds: 31));

	[Fact]
	public void DoesNotHold_ACompleteSet() =>
		Assert.False(GeyserFillWait.Holds(live: 114, saved: 114, stableSeconds: 0, elapsedSeconds: 0));
}
