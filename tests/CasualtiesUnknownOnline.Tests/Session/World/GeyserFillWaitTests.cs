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

	// The ceiling scales with the archive's own size (a 115-row archive needs longer than a 20-row
	// one), so the release point is asked of the rule rather than written here twice.
	[Fact]
	public void Releases_AtTheCeilingWhileTheSetStillGrows() =>
		Assert.False(GeyserFillWait.Holds(
			live: 94,
			saved: 114,
			stableSeconds: 0.1,
			elapsedSeconds: GeyserFillWait.CeilingFor(114) + 1));

	// The 2026-11-25 reading: 115 archive rows, the live set two thirds full at 30 s. The old flat
	// 30 s ceiling cut the restore off there; the scaled one keeps it waiting.
	[Fact]
	public void CeilingFor_GrowsWithTheArchive() =>
		Assert.True(GeyserFillWait.CeilingFor(115) > 30);

	[Fact]
	public void DoesNotHold_ACompleteSet() =>
		Assert.False(GeyserFillWait.Holds(live: 114, saved: 114, stableSeconds: 0, elapsedSeconds: 0));
}
