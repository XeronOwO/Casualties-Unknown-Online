using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.CharacterData;

/// <summary>
/// The owner's death count (the user's decision 2026-11-14): the game's own <c>Body.alive</c> edge, one
/// count per genuine alive → dead transition. A fresh spawn, a restore, a world entry and a frame without
/// a body must never count, or a continue would inflate a character's deaths.
/// </summary>
public class PlayerDeathCounterTests
{
	[Fact]
	public void AFreshAliveBody_CountsNothing()
	{
		var counter = new PlayerDeathCounter();

		counter.Tick(alive: true);
		counter.Tick(alive: true);

		Assert.Equal(0, counter.Count);
	}

	[Fact]
	public void AnAliveToDeadEdge_CountsOnce_AndStayingDeadDoesNotCountAgain()
	{
		var counter = new PlayerDeathCounter();

		counter.Tick(true);
		counter.Tick(false);
		counter.Tick(false);
		counter.Tick(false);

		Assert.Equal(1, counter.Count);
	}

	[Fact]
	public void AReviveRearmsTheEdge_SoTheSecondDeathCounts()
	{
		var counter = new PlayerDeathCounter();

		counter.Tick(true);
		counter.Tick(false);
		counter.Tick(true);
		counter.Tick(false);

		Assert.Equal(2, counter.Count);
	}

	[Fact]
	public void AFrameWithoutALocalBody_DisarmsTheEdge_AndAQuietDeathDoesNotCount()
	{
		var counter = new PlayerDeathCounter();

		// No body was ever seen alive here: a dead reading cannot be a death of this character.
		counter.Tick(alive: null);
		counter.Tick(alive: false);

		Assert.Equal(0, counter.Count);
	}

	[Fact]
	public void ARestoredCountIsWrittenBack_AndTheRestoreSpawnIsNotADeath()
	{
		var counter = new PlayerDeathCounter();
		counter.Tick(true);
		counter.Tick(false);
		Assert.Equal(1, counter.Count);

		// The snapshot the host hands back names the character's own count.
		counter.SetCount(4);
		Assert.Equal(4, counter.Count);

		// The restore rebuilds a body: a frame without it, then the fresh alive body, then no death yet.
		counter.Tick(null);
		counter.Tick(true);
		Assert.Equal(4, counter.Count);

		counter.Tick(false);
		Assert.Equal(5, counter.Count);
	}

	[Fact]
	public void Reset_StartsTheNextRunAtZero()
	{
		var counter = new PlayerDeathCounter();
		counter.Tick(true);
		counter.Tick(false);
		counter.Tick(true);
		Assert.Equal(1, counter.Count);

		counter.Reset();
		counter.Tick(true);

		Assert.Equal(0, counter.Count);
	}

	[Fact]
	public void ANegativeRestoredCount_IsClampedToZero()
	{
		var counter = new PlayerDeathCounter();

		counter.SetCount(-3);

		Assert.Equal(0, counter.Count);
	}
}
