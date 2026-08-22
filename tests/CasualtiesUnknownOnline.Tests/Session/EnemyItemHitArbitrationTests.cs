using System;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The pure item-vs-enemy hit rules: the same damage formulas and the
/// multiplayer generalization of the native 50-unit proximity guard
/// (SpiderHandler.cs:246-258). L0 coverage replaces manual dual-open
/// acceptance of the calculation layer; the Unity patch boundary is locked by
/// PatchContractTests + GameFieldContractTests.
/// </summary>
public class EnemyItemHitArbitrationTests
{
	private static NetVector2 Pos(float x, float y) => new(x, y);

	[Fact]
	public void ComputeImpactWeight_MatchesNativeSpeedTimesClampedMass() =>
		Assert.True(Math.Abs(EnemyItemHitArbitration.ComputeImpactWeight(4f, 3f) - 12f) < 0.001f);

	[Fact]
	public void ComputeImpactWeight_ClampsMassAboveFour() =>
		Assert.True(
			Math.Abs(EnemyItemHitArbitration.ComputeImpactWeight(4f, 8f) - 16f) < 0.001f
			&& EnemyItemHitArbitration.ComputeImpactWeight(4f, -1f) == 0f);

	[Fact]
	public void ComputeHealthDamage_MatchesNativeZeroPointSixSixFactor() =>
		// num = 4 * 3 = 12; health -= 12 * 0.66 = 7.92 (SpiderHandler.cs:249/254)
		Assert.True(Math.Abs(EnemyItemHitArbitration.ComputeHealthDamage(4f, 3f) - 7.92f) < 0.001f);

	[Fact]
	public void ComputeStunDamage_MatchesNativeOnePointFiveFactor() =>
		// num = 4 * 3 = 12; AnimalHit(12 * 1.5 = 18) (SpiderHandler.cs:249/256)
		Assert.True(Math.Abs(EnemyItemHitArbitration.ComputeStunDamage(4f, 3f) - 18f) < 0.001f);

	[Theory]
	[InlineData(2.001f, true)]
	[InlineData(2f, false)]
	[InlineData(1.999f, false)]
	public void IsImpactEligible_OnlyAboveNativeMinimumSpeed(float speed, bool expected) =>
		Assert.Equal(expected, EnemyItemHitArbitration.IsImpactEligible(speed));

	[Fact]
	public void AnyPlayerWithin_DetectsNearbyPlayer() =>
		Assert.True(EnemyItemHitArbitration.AnyPlayerWithin(
			[Pos(0f, 0f), Pos(15f, 10f)], Pos(10f, 10f), 50f));

	[Fact]
	public void AnyPlayerWithin_ReturnsFalseWhenAllPlayersAreFar() =>
		Assert.False(EnemyItemHitArbitration.AnyPlayerWithin(
			[Pos(0f, 0f), Pos(200f, 200f)], Pos(100f, 100f), 50f));

	[Fact]
	public void AnyPlayerWithin_EmptySetIsFalse() =>
		Assert.False(EnemyItemHitArbitration.AnyPlayerWithin([], Pos(0f, 0f), 50f));
}
