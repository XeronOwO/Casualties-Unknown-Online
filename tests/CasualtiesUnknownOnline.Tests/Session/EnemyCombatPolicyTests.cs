using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Locks the pure enemy-combat policy constants extracted from
/// <c>EnemyCombatDirector</c>. Keeping the thresholds in a Runtime policy class
/// makes the values part of the testable surface and prepares the decisions for a
/// future kernel process. The host's own lunge-ray slack lived here while the
/// host decided the lunge hit; that decision now belongs to the client the effect
/// lands on (the game's own ray, run locally), so only the two thresholds the
/// host still uses remain.
/// </summary>
public class EnemyCombatPolicyTests
{
	[Fact]
	public void SpiderBiteRange_MatchesTheGameContactRadius() =>
		Assert.Equal(1.5f, EnemyCombatPolicy.SpiderBiteRange);

	[Fact]
	public void CrystalCloseRange_MatchesTheGameProximityRadius() =>
		Assert.Equal(64f, EnemyCombatPolicy.CrystalCloseRange);
}
