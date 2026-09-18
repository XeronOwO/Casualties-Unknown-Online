using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The pure VICTIM-side attack judgment: the client the announced attack may have
/// landed on decides whether it did, from its own view of the enemy and its own
/// body. The physics probing (collider contact, the crystal's ray) stays in the
/// Game Adapter's probe and the Unity boundary stays behind contracts — this is
/// the rule that answers "which of my limbs did that attack reach", so a screen
/// that shows a dodge is a dodge and no latency value enters the decision.
/// </summary>
public class EnemyAttackJudgmentTests
{
	private static EnemyAttackJudgment.BiteContact Contact(int index, float x, float y, bool touching = true) =>
		new(index, new NetVector2(x, y), touching);

	[Fact]
	public void SelectBittenLimb_TouchingLimbInFront_IsBitten()
	{
		var limb = EnemyAttackJudgment.SelectBittenLimb(
			new NetVector2(0f, 0f), new NetVector2(1f, 0f), minVectorDotToBite: -1f,
			[Contact(3, 1f, 0f)]);

		Assert.Equal(3, limb);
	}

	[Fact]
	public void SelectBittenLimb_NonTouchingLimb_IsNeverBitten()
	{
		var limb = EnemyAttackJudgment.SelectBittenLimb(
			new NetVector2(0f, 0f), new NetVector2(1f, 0f), -1f,
			[Contact(1, 1f, 0f, touching: false)]);

		Assert.Equal(-1, limb);
	}

	[Fact]
	public void SelectBittenLimb_NearestTouchingLimbWins()
	{
		var limb = EnemyAttackJudgment.SelectBittenLimb(
			new NetVector2(0f, 0f), new NetVector2(1f, 0f), -1f,
			[Contact(1, 4f, 0f), Contact(2, 1f, 0f), Contact(0, 2f, 0f)]);

		Assert.Equal(2, limb);
	}

	[Fact]
	public void SelectBittenLimb_FacingGateBlocksALimbTheSpiderHasItsBackTo()
	{
		var limb = EnemyAttackJudgment.SelectBittenLimb(
			new NetVector2(0f, 0f), new NetVector2(1f, 0f), minVectorDotToBite: 0.2f,
			[Contact(1, -1f, 0f)]); // the limb sits behind the enemy's facing

		Assert.Equal(-1, limb);
	}

	[Fact]
	public void SelectBittenLimb_TheGateIsStrict_AnExactlySideOnLimbIsNotBitten()
	{
		var limb = EnemyAttackJudgment.SelectBittenLimb(
			new NetVector2(0f, 0f), new NetVector2(1f, 0f), minVectorDotToBite: 0f,
			[Contact(1, 0f, 2f)]); // perpendicular to the facing: dot == 0 == the gate

		Assert.Equal(-1, limb);
	}

	[Fact]
	public void SelectBittenLimb_LimbOnTheEnemy_CountsAsFaced()
	{
		var limb = EnemyAttackJudgment.SelectBittenLimb(
			new NetVector2(2f, 2f), new NetVector2(1f, 0f), minVectorDotToBite: 0.9f,
			[Contact(0, 2f, 2f)]);

		Assert.Equal(0, limb);
	}

	[Fact]
	public void SelectBittenLimb_NoContacts_IsAMiss()
	{
		var limb = EnemyAttackJudgment.SelectBittenLimb(
			new NetVector2(0f, 0f), new NetVector2(1f, 0f), -1f, []);

		Assert.Equal(-1, limb);
	}

	[Fact]
	public void LungeHitsLocalBody_FirstBodyIsThisClient_IsAHit()
	{
		var hit = EnemyAttackJudgment.LungeHitsLocalBody(
		[
			new EnemyAttackJudgment.LungeHit(isBody: false, isLocalBody: false, isGround: false),
			new EnemyAttackJudgment.LungeHit(isBody: true, isLocalBody: true, isGround: false),
			new EnemyAttackJudgment.LungeHit(isBody: false, isLocalBody: false, isGround: true),
		]);

		Assert.True(hit);
	}

	[Fact]
	public void LungeHitsLocalBody_GroundBeforeEveryBody_IsAMiss()
	{
		var hit = EnemyAttackJudgment.LungeHitsLocalBody(
		[
			new EnemyAttackJudgment.LungeHit(isBody: false, isLocalBody: false, isGround: true),
			new EnemyAttackJudgment.LungeHit(isBody: true, isLocalBody: true, isGround: false),
		]);

		Assert.False(hit, "the crystal stops at the ground it reaches first");
	}

	[Fact]
	public void LungeHitsLocalBody_AnotherBodyIsFirst_IsAMiss()
	{
		var hit = EnemyAttackJudgment.LungeHitsLocalBody(
		[
			new EnemyAttackJudgment.LungeHit(isBody: true, isLocalBody: false, isGround: false),
			new EnemyAttackJudgment.LungeHit(isBody: true, isLocalBody: true, isGround: false),
		]);

		Assert.False(hit, "the game damages the FIRST body the ray meets, and only that one");
	}

	[Fact]
	public void LungeHitsLocalBody_NoBodyOnTheRay_IsAMiss()
	{
		var hit = EnemyAttackJudgment.LungeHitsLocalBody(
			[new EnemyAttackJudgment.LungeHit(isBody: false, isLocalBody: false, isGround: true)]);

		Assert.False(hit);
	}
}
