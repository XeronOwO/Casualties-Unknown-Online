using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The victim-side attack identity ledger: one announced attack applies at most
/// once, whatever the transport does with the message. This is what makes "the
/// attack was announced twice" and "an older announcement arrived late" harmless
/// without any window keyed on the peer's latency.
/// </summary>
public class EnemyAttackLedgerTests
{
	private static readonly NetworkEntityId Enemy = new(7, 3, 0);
	private static readonly NetworkEntityId OtherEnemy = new(7, 4, 0);

	[Fact]
	public void FirstAnnouncement_IsJudged()
	{
		var ledger = new EnemyAttackLedger();

		Assert.True(ledger.ShouldJudge(Enemy, 1));
	}

	[Fact]
	public void RepeatedAnnouncement_IsNotJudgedTwice()
	{
		var ledger = new EnemyAttackLedger();

		Assert.True(ledger.ShouldJudge(Enemy, 1));
		Assert.False(ledger.ShouldJudge(Enemy, 1));
	}

	[Fact]
	public void StaleAnnouncement_ArrivingAfterANewerOne_IsIgnored()
	{
		var ledger = new EnemyAttackLedger();

		Assert.True(ledger.ShouldJudge(Enemy, 5));
		Assert.False(ledger.ShouldJudge(Enemy, 4));
	}

	[Fact]
	public void ANewerAnnouncement_IsJudged_EvenWhenThePreviousOneMissed()
	{
		var ledger = new EnemyAttackLedger();

		Assert.True(ledger.ShouldJudge(Enemy, 5));
		Assert.True(ledger.ShouldJudge(Enemy, 6));
	}

	[Fact]
	public void TwoEnemies_KeepSeparateIdentities()
	{
		var ledger = new EnemyAttackLedger();

		Assert.True(ledger.ShouldJudge(Enemy, 3));
		Assert.True(ledger.ShouldJudge(OtherEnemy, 1));
		Assert.False(ledger.ShouldJudge(OtherEnemy, 1));
	}

	[Fact]
	public void MissingIdentity_FailsClosed()
	{
		var ledger = new EnemyAttackLedger();

		Assert.False(ledger.ShouldJudge(Enemy, 0));
		Assert.True(ledger.ShouldJudge(Enemy, 1), "a malformed announcement must not consume the enemy's identity");
	}

	[Fact]
	public void AnotherGenerationOfTheSameCounter_IsADifferentEnemy()
	{
		var ledger = new EnemyAttackLedger();

		Assert.True(ledger.ShouldJudge(Enemy, 9));
		Assert.True(ledger.ShouldJudge(new NetworkEntityId(8, 3, 0), 1), "the enemy id carries the epoch, so a new generation starts fresh");
	}
}
