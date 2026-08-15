using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The runtime enemy-spawn binding arbitration (EnemyRuntimeSpawnArbitration):
/// live 20 Hz binding pairs unbound host states with the local copies created
/// by the EntitySpawned channel, all-or-nothing by position; the late-joiner
/// snapshot matching greedily binds same-prefab copies and leaves the missing
/// ones as the materialization list. Pure logic — no Unity scene.
/// </summary>
public class EnemyRuntimeSpawnArbitrationTests
{
	[Fact]
	public void TryPairByPosition_IdenticalSets_PairsByPositionOrder_RegardlessOfInputOrder()
	{
		NetVector2[] states = [new(2f, 1f), new(0f, 0f), new(1f, 1f)];
		NetVector2[] candidates = [new(1f, 1f), new(2f, 1f), new(0f, 0f)];

		var ok = EnemyRuntimeSpawnArbitration.TryPairByPosition(states, candidates, out var pairs);

		Assert.True(ok);
		Assert.Equal(3, pairs.Count);
		Assert.Equal([1, 2, 0], pairs.Select(p => p.StateIndex).ToArray());
		Assert.Equal([2, 0, 1], pairs.Select(p => p.CandidateIndex).ToArray());
	}

	[Fact]
	public void TryPairByPosition_CountMismatch_Fails()
	{
		var ok = EnemyRuntimeSpawnArbitration.TryPairByPosition(
			[new NetVector2(0f, 0f), new NetVector2(1f, 1f)],
			[new NetVector2(0f, 0f)],
			out var pairs);

		Assert.False(ok);
		Assert.Empty(pairs);
	}

	[Fact]
	public void TryPairByPosition_OutOfTolerance_Fails()
	{
		var ok = EnemyRuntimeSpawnArbitration.TryPairByPosition(
			[new NetVector2(0f, 0f)],
			[new NetVector2(1f, 0f)],
			out var pairs);

		Assert.False(ok, "a runtime copy farther than the spawn tolerance must not be bound");
		Assert.Empty(pairs);
	}

	[Fact]
	public void TryPairByPosition_Empty_Fails()
	{
		var ok = EnemyRuntimeSpawnArbitration.TryPairByPosition([], [], out var pairs);

		Assert.False(ok);
		Assert.Empty(pairs);
	}

	[Fact]
	public void MatchRuntimeSpawns_NearestSamePrefab_OneToOne_AndMaterializesTheRest()
	{
		var spawns = new[]
		{
			(SpawnIndex: 0, PrefabId: "cavetick", Position: new NetVector2(0f, 0f)),
			(SpawnIndex: 1, PrefabId: "cavetick", Position: new NetVector2(0.2f, 0f)),
			(SpawnIndex: 2, PrefabId: "cavetick", Position: new NetVector2(9f, 0f)),
		};
		var candidates = new[]
		{
			(CandidateIndex: 0, PrefabId: "shadecrawler", Position: new NetVector2(0f, 0f)),
			(CandidateIndex: 1, PrefabId: "cavetick", Position: new NetVector2(0f, 0.1f)),
			(CandidateIndex: 2, PrefabId: "cavetick", Position: new NetVector2(0.3f, 0f)),
		};

		EnemyRuntimeSpawnArbitration.MatchRuntimeSpawns(spawns, candidates, out var pairs, out var unmatched);

		Assert.Equal(2, pairs.Count);
		Assert.Contains((0, 1), pairs); // nearest cavetick copy to (0,0)
		Assert.Contains((1, 2), pairs); // nearest cavetick copy to (0.2,0)
		Assert.Equal([2], unmatched); // no copy near (9,0) — materialize
	}

	[Fact]
	public void MatchRuntimeSpawns_Empty_ProducesNoPairs()
	{
		EnemyRuntimeSpawnArbitration.MatchRuntimeSpawns([], [], out var pairs, out var unmatched);

		Assert.Empty(pairs);
		Assert.Empty(unmatched);
	}
}
