using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.World;
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

	[Fact]
	public void MatchRuntimeSpawnsByCreationKey_BindsByKey_RegardlessOfDistance()
	{
		// The identity pass: a fact the host attributed to a creation record binds
		// the copy carrying that key even when it has drifted far outside the
		// positional tolerance — otherwise the snapshot materializes a SECOND
		// copy with the same key.
		var key = new RuntimeEntityKey("cavetick", 0, 0, 2001, 3);
		var spawns = new[] { (SpawnIndex: 0, CreationKey: (RuntimeEntityKey?)key) };
		var candidates = new[] { (CandidateIndex: 0, CreationKey: (RuntimeEntityKey?)key) };

		EnemyRuntimeSpawnArbitration.MatchRuntimeSpawnsByCreationKey(spawns, candidates, out var pairs, out var unmatched);

		Assert.Equal([(0, 0)], pairs);
		Assert.Empty(unmatched);
	}

	[Fact]
	public void MatchRuntimeSpawnsByCreationKey_KeyedFact_DoesNotBindAnUnrelatedKeylessCopy()
	{
		// The fact has an identity and the local copy does not: it must be
		// materialized, never positionally absorbed into the unrelated copy.
		var key = new RuntimeEntityKey("cavetick", 0, 0, 2001, 3);
		var spawns = new[] { (SpawnIndex: 0, CreationKey: (RuntimeEntityKey?)key) };
		var candidates = new[] { (CandidateIndex: 0, CreationKey: (RuntimeEntityKey?)null) };

		EnemyRuntimeSpawnArbitration.MatchRuntimeSpawnsByCreationKey(spawns, candidates, out var pairs, out var unmatched);

		Assert.Empty(pairs);
		Assert.Equal([0], unmatched);
	}

	[Fact]
	public void MatchRuntimeSpawnsByCreationKey_KeylessFact_IsLeftForThePositionalPass()
	{
		// A fact the host could not attribute has no identity; it stays out of
		// this pass (the caller's positional fallback handles it) and never
		// consumes a keyed candidate.
		var key = new RuntimeEntityKey("cavetick", 0, 0, 2001, 3);
		var spawns = new[] { (SpawnIndex: 0, CreationKey: (RuntimeEntityKey?)null) };
		var candidates = new[] { (CandidateIndex: 0, CreationKey: (RuntimeEntityKey?)key) };

		EnemyRuntimeSpawnArbitration.MatchRuntimeSpawnsByCreationKey(spawns, candidates, out var pairs, out var unmatched);

		Assert.Empty(pairs);
		Assert.Empty(unmatched);
	}

	[Fact]
	public void MatchRuntimeSpawnsByCreationKey_TwoFactsOneKeyedCopy_SecondIsMaterialized()
	{
		// One keyed copy can serve one fact only; the other fact (same key —
		// a duplicate snapshot) must materialize rather than share the copy.
		var key = new RuntimeEntityKey("cavetick", 0, 0, 2001, 3);
		var spawns = new[]
		{
			(SpawnIndex: 0, CreationKey: (RuntimeEntityKey?)key),
			(SpawnIndex: 1, CreationKey: (RuntimeEntityKey?)key),
		};
		var candidates = new[] { (CandidateIndex: 0, CreationKey: (RuntimeEntityKey?)key) };

		EnemyRuntimeSpawnArbitration.MatchRuntimeSpawnsByCreationKey(spawns, candidates, out var pairs, out var unmatched);

		Assert.Equal([(0, 0)], pairs);
		Assert.Equal([1], unmatched);
	}

	[Fact]
	public void MatchRuntimeSpawnsByIdentity_MixedKeyedAndKeyless_ReportsRealCandidateIndices()
	{
		// The orchestration filters the candidate list between the two passes,
		// so the positional pass's result MUST be mapped back to the caller's
		// candidate index space. A keyed candidate must never be consumed by a
		// keyless fact, and the keyless fact must bind its OWN copy.
		var key = new RuntimeEntityKey("cavetick", 0, 0, 2001, 3);
		var spawns = new[]
		{
			(SpawnIndex: 0, PrefabId: "cavetick", Position: new NetVector2(0f, 0f), CreationKey: (RuntimeEntityKey?)key),
			(SpawnIndex: 1, PrefabId: "cavetick", Position: new NetVector2(0.1f, 0f), CreationKey: (RuntimeEntityKey?)null),
		};
		var candidates = new[]
		{
			(CandidateIndex: 0, PrefabId: "cavetick", Position: new NetVector2(0f, 0f), CreationKey: (RuntimeEntityKey?)key),
			(CandidateIndex: 1, PrefabId: "cavetick", Position: new NetVector2(0.1f, 0f), CreationKey: (RuntimeEntityKey?)null),
		};

		EnemyRuntimeSpawnArbitration.MatchRuntimeSpawnsByIdentity(spawns, candidates, out var pairs, out var unmatched);

		Assert.Equal(2, pairs.Count);
		Assert.Contains((0, 0), pairs); // the keyed fact binds the keyed copy
		Assert.Contains((1, 1), pairs); // the keyless fact binds the keyless copy, NOT candidate 0
		Assert.Empty(unmatched);
	}

	[Fact]
	public void MatchRuntimeSpawnsByIdentity_KeyedFactWithNoKeyedCopy_IsMaterialized()
	{
		// The keyed fact must never fall back to a keyless copy by position.
		var key = new RuntimeEntityKey("cavetick", 0, 0, 2001, 3);
		var spawns = new[]
		{
			(SpawnIndex: 0, PrefabId: "cavetick", Position: new NetVector2(0f, 0f), CreationKey: (RuntimeEntityKey?)key),
		};
		var candidates = new[]
		{
			(CandidateIndex: 0, PrefabId: "cavetick", Position: new NetVector2(0f, 0f), CreationKey: (RuntimeEntityKey?)null),
		};

		EnemyRuntimeSpawnArbitration.MatchRuntimeSpawnsByIdentity(spawns, candidates, out var pairs, out var unmatched);

		Assert.Empty(pairs);
		Assert.Equal([0], unmatched);
	}

	[Fact]
	public void MatchRuntimeSpawnsByIdentity_Empty_ProducesNothing()
	{
		EnemyRuntimeSpawnArbitration.MatchRuntimeSpawnsByIdentity([], [], out var pairs, out var unmatched);

		Assert.Empty(pairs);
		Assert.Empty(unmatched);
	}
}
