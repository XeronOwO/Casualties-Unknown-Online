using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session.World;

/// <summary>
/// The wait's rule without a clock: a set that is still short of the archive is held until the
/// ceiling, a set that reaches the archive's count is released at once, and a set that never
/// arrives is released at the ceiling so its refused rows are reported.
/// </summary>
public sealed class GeyserFillWaitTests
{
	// The 2026-11-17 reading: 94 of 114 objects five seconds past the seam.
	[Fact]
	public void Holds_WhileTheLiveSetIsStillGrowing() =>
		Assert.True(GeyserFillWait.Holds(live: 94, saved: 114, stableSeconds: 0.5, elapsedSeconds: 5));

	// The 2026-11-25 reading: a three-second pause between chunk bursts with 96 of 115
	// objects in place. The pause is not completion, so the restore keeps waiting.
	[Fact]
	public void Holds_ThroughAPauseThatIsShortOfTheArchive() =>
		Assert.True(GeyserFillWait.Holds(live: 96, saved: 115, stableSeconds: 3.5, elapsedSeconds: 8));

	[Fact]
	public void Releases_AtTheCeilingWhileTheSetStillGrows() =>
		Assert.False(GeyserFillWait.Holds(live: 94, saved: 114, stableSeconds: 0.1, elapsedSeconds: 31));

	[Fact]
	public void DoesNotHold_ACompleteSet() =>
		Assert.False(GeyserFillWait.Holds(live: 114, saved: 114, stableSeconds: 0, elapsedSeconds: 0));
}
