using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Locks the pure enemy-combat policy constants extracted from
/// <c>EnemyCombatDirector</c>. Keeping the thresholds in a Runtime policy class
/// makes the values part of the testable arbitration surface and prepares the
/// decisions for a future kernel process.
/// </summary>
public class EnemyCombatPolicyTests
{
	[Fact]
	public void SpiderBiteRange_MatchesTheGameContactRadius() =>
		Assert.Equal(1.5f, EnemyCombatPolicy.SpiderBiteRange);

	[Fact]
	public void CrystalCloseRange_MatchesTheGameProximityRadius() =>
		Assert.Equal(64f, EnemyCombatPolicy.CrystalCloseRange);

	[Fact]
	public void CrystalRayLength_MatchesTheGameLungeRaycast() =>
		Assert.Equal(999f, EnemyCombatPolicy.CrystalRayLength);

	[Fact]
	public void CrystalRayTolerance_IsTheHostLungeAcceptanceSlack() =>
		Assert.Equal(2f, EnemyCombatPolicy.CrystalRayTolerance);
}
