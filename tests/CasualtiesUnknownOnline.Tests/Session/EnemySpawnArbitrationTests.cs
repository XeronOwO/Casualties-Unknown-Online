using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The enemy-spawn pairing arbitration (EnemySpawnArbitration): both sides
/// generate the same animal entities deterministically but hold separate
/// instances — the (x, y) sort is the deterministic allocation order, and the
/// index pairing + tolerance check is what catches a generation divergence
/// instead of silently mispairing enemies.
/// </summary>
public class EnemySpawnArbitrationTests
{
	[Fact]
	public void Order_SortsByXThenY()
	{
		var input = new[]
		{
			new NetVector2(3f, 0f),
			new NetVector2(1f, 5f),
			new NetVector2(1f, 2f),
			new NetVector2(2f, 0f),
		};

		var ordered = EnemySpawnArbitration.Order(input);

		Assert.Equal(4, ordered.Count);
		Assert.Equal(new NetVector2(1f, 2f), ordered[0]);
		Assert.Equal(new NetVector2(1f, 5f), ordered[1]);
		Assert.Equal(new NetVector2(2f, 0f), ordered[2]);
		Assert.Equal(new NetVector2(3f, 0f), ordered[3]);
	}

	[Fact]
	public void Order_IsDeterministic_RegardlessOfInputOrder()
	{
		var shuffled = new[]
		{
			new NetVector2(2f, 1f),
			new NetVector2(0f, 0f),
			new NetVector2(1f, 1f),
		};
		var sortedInput = new[]
		{
			new NetVector2(0f, 0f),
			new NetVector2(1f, 1f),
			new NetVector2(2f, 1f),
		};

		var fromShuffled = EnemySpawnArbitration.Order(shuffled);
		var fromSorted = EnemySpawnArbitration.Order(sortedInput);

		Assert.Equal(fromSorted, fromShuffled);
	}

	[Fact]
	public void Order_Empty_ReturnsEmpty() =>
		Assert.Empty(EnemySpawnArbitration.Order([]));

	[Fact]
	public void TryPair_IdenticalPositions_PairsIndexByIndex()
	{
		var host = new[] { new NetVector2(0f, 0f), new NetVector2(1f, 1f), new NetVector2(2f, 2f) };
		var guest = new[] { new NetVector2(0f, 0f), new NetVector2(1f, 1f), new NetVector2(2f, 2f) };

		var ok = EnemySpawnArbitration.TryPair(host, guest, out var pairs);

		Assert.True(ok);
		Assert.Equal(3, pairs.Count);
		for (var i = 0; i < pairs.Count; i++)
		{
			Assert.Equal(i, pairs[i].HostIndex);
			Assert.Equal(i, pairs[i].GuestIndex);
		}
	}

	[Fact]
	public void TryPair_CountMismatch_Fails()
	{
		var host = new[] { new NetVector2(0f, 0f), new NetVector2(1f, 1f) };
		var guest = new[] { new NetVector2(0f, 0f) };

		var ok = EnemySpawnArbitration.TryPair(host, guest, out var pairs);

		Assert.False(ok);
		Assert.Empty(pairs);
	}

	[Fact]
	public void TryPair_OutOfTolerance_Fails()
	{
		var host = new[] { new NetVector2(0f, 0f) };
		var guest = new[] { new NetVector2(1f, 0f) }; // 1.0 world units > the 0.5 tolerance

		var ok = EnemySpawnArbitration.TryPair(host, guest, out _);

		Assert.False(ok, "a divergent spawn position must not be silently mispaired");
	}

	[Fact]
	public void TryPair_WithinTolerance_Pairs()
	{
		var host = new[] { new NetVector2(0f, 0f) };
		var guest = new[] { new NetVector2(0.4f, 0f) }; // < the 0.5 tolerance

		var ok = EnemySpawnArbitration.TryPair(host, guest, out var pairs);

		Assert.True(ok);
		Assert.Single(pairs);
		Assert.True(pairs[0].Distance < EnemySpawnArbitration.PairTolerance);
	}
}
