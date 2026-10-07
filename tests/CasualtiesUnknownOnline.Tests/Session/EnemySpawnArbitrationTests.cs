using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The enemy-spawn pairing arbitration (EnemySpawnArbitration): both sides
/// generate the same animal entities deterministically but hold separate
/// instances — the (x, y) sort is the deterministic allocation order, and the
/// index pairing + tolerance check is what catches a generation divergence
/// instead of silently mispairing enemies. The generated-set entry point takes
/// the host FACTS rather than a position list, so the key it pairs on is the
/// bind-time anchor they carry: these cases pin that the live pose can never
/// become the key, whatever the caller holds (batch `20261006-g` read the cost
/// of a caller that passed the live pose).
/// </summary>
public class EnemySpawnArbitrationTests
{
	private static EnemyEntity HostFact(uint counter, NetVector2 spawn, NetVector2? live = null) =>
		new(new NetworkEntityId(1, counter, 0))
		{
			SpawnPosition = spawn,
			Position = live ?? spawn,
		};

	[Fact]
	public void TryPairGeneratedCopies_OrdersBothSidesByTheAnchor_AndReportsTheCallersCopyIndexes()
	{
		var facts = new[]
		{
			HostFact(7, new NetVector2(3f, 0f)),
			HostFact(4, new NetVector2(1f, 5f)),
			HostFact(9, new NetVector2(1f, 2f)),
		};
		var copies = new[]
		{
			new NetVector2(1f, 2f), // the caller's index 0
			new NetVector2(3f, 0f), // 1
			new NetVector2(1f, 5f), // 2
		};

		var ok = EnemySpawnArbitration.TryPairGeneratedCopies(facts, copies, out var pairs, out _);

		Assert.True(ok);
		Assert.Equal(3, pairs.Count);

		// (1, 2) < (1, 5) < (3, 0): the rank order is the anchor order on BOTH
		// sides, and the copy index is the caller's own list position — the
		// caller binds by that index, never by rank.
		Assert.Equal(9u, pairs[0].Host.EntityId.Counter);
		Assert.Equal(0, pairs[0].CopyIndex);
		Assert.Equal(4u, pairs[1].Host.EntityId.Counter);
		Assert.Equal(2, pairs[1].CopyIndex);
		Assert.Equal(7u, pairs[2].Host.EntityId.Counter);
		Assert.Equal(1, pairs[2].CopyIndex);

		// The same host set in any input ORDER produces the same pairing: the
		// arbitration does the ordering, not the caller. (The copy indices are the
		// caller's own list positions, so that list is deliberately not shuffled
		// here — a shuffle there would move them, which is what the index report
		// exists for.)
		var shuffled = EnemySpawnArbitration.TryPairGeneratedCopies(
			[facts[2], facts[0], facts[1]],
			copies,
			out var reordered,
			out _);

		Assert.True(shuffled);
		Assert.Equal(
			pairs.Select(p => (p.Host.EntityId.Counter, p.CopyIndex)).ToList(),
			reordered.Select(p => (p.Host.EntityId.Counter, p.CopyIndex)).ToList());
	}

	[Fact]
	public void TryPairGeneratedCopies_LateJoin_PairsOnTheAnchorTheLivePoseHasLeft()
	{
		// Batch `20261006-g`: the third client entered the world about two
		// minutes into the run, so the host's animals had long left the positions
		// they were generated at, while the member's own copies are frozen at
		// THEIRS — the same positions. The set pairs on the anchor and cannot
		// pair on the live pose; the distance assertion below is what makes THIS
		// case fail if the key ever reads Position again.
		var facts = new[]
		{
			HostFact(1, spawn: new NetVector2(10f, 20f), live: new NetVector2(64f, 88f)),
			HostFact(2, spawn: new NetVector2(40f, 55f), live: new NetVector2(12f, 70f)),
		};
		var frozenCopies = new[]
		{
			new NetVector2(40f, 55f),
			new NetVector2(10f, 20f),
		};

		Assert.All(facts, fact => Assert.True(
			EnemySpawnArbitration.Distance(fact.SpawnPosition, fact.Position) > EnemySpawnArbitration.PairTolerance,
			"this case carries a live pose beyond tolerance of the anchor — a live-keyed pairing cannot pass it"));

		var ok = EnemySpawnArbitration.TryPairGeneratedCopies(facts, frozenCopies, out var pairs, out _);

		Assert.True(ok, "a late joiner pairs on the host's bind-time anchors, not on where its animals have wandered");
		Assert.Equal(2, pairs.Count);
		Assert.Equal(1u, pairs[0].Host.EntityId.Counter);
		Assert.Equal(1, pairs[0].CopyIndex);
		Assert.Equal(2u, pairs[1].Host.EntityId.Counter);
		Assert.Equal(0, pairs[1].CopyIndex);
	}

	[Fact]
	public void TryPairGeneratedCopies_CountMismatch_ReportsBothCounts()
	{
		var ok = EnemySpawnArbitration.TryPairGeneratedCopies(
			[HostFact(1, new NetVector2(0f, 0f))],
			[new NetVector2(0f, 0f), new NetVector2(1f, 1f)],
			out var pairs,
			out var divergence);

		Assert.False(ok, "the whole set pairs or none of it does");
		Assert.Empty(pairs);
		Assert.True(divergence.IsCountMismatch, "a count mismatch has no index to report");
		Assert.Equal(EnemySpawnArbitration.GeneratedPairingOutcome.CountMismatch, divergence.Outcome);
		Assert.Equal(1, divergence.HostCount);
		Assert.Equal(2, divergence.CopyCount);
	}

	[Fact]
	public void TryPairGeneratedCopies_OutOfTolerance_ReportsTheFirstIndexAndItsDistance()
	{
		var facts = new[]
		{
			HostFact(1, new NetVector2(0f, 0f)),
			HostFact(2, new NetVector2(10f, 0f)),
		};
		var copies = new[]
		{
			new NetVector2(0.25f, 0f), // rank 0: inside the tolerance
			new NetVector2(12f, 0f), // rank 1: 2.0 world units out
		};

		var ok = EnemySpawnArbitration.TryPairGeneratedCopies(facts, copies, out var pairs, out var divergence);

		Assert.False(ok, "one divergent copy fails the whole set");
		Assert.Empty(pairs);
		Assert.False(divergence.IsCountMismatch);
		Assert.Equal(EnemySpawnArbitration.GeneratedPairingOutcome.KeyMismatch, divergence.Outcome);
		Assert.Equal(1, divergence.FirstMismatchIndex);
		Assert.Equal(2f, divergence.FirstMismatchDistance, 3);
		Assert.Equal(2, divergence.HostCount);
		Assert.Equal(2, divergence.CopyCount);
	}

	[Fact]
	public void TryPairGeneratedCopies_WithinTolerance_PairsAndReportsTheKeyDistance()
	{
		var ok = EnemySpawnArbitration.TryPairGeneratedCopies(
			[HostFact(1, new NetVector2(0f, 0f))],
			[new NetVector2(0.4f, 0f)], // < the 0.5 tolerance
			out var pairs,
			out var divergence);

		Assert.True(ok);
		Assert.Single(pairs);
		Assert.Equal(0.4f, pairs[0].Distance, 3);
		Assert.True(pairs[0].Distance < EnemySpawnArbitration.PairTolerance);
		Assert.Equal(EnemySpawnArbitration.GeneratedPairingOutcome.Paired, divergence.Outcome);
		Assert.False(divergence.IsCountMismatch, "a successful pair is not a count mismatch — the outcome field, not the index sentinel, says so");
	}

	[Theory]
	[InlineData(false, true, 0, true)] // the repair paired the remaining copies
	[InlineData(true, false, 0, true)] // nothing left to pair — the established baseline is preserved
	[InlineData(true, false, 1, false)] // a copy still needs pairing and did not pair — generation divergence
	[InlineData(false, false, 0, false)] // nothing paired and nothing was ever established
	[InlineData(false, false, 3, false)]
	public void ShouldRepairGenerationBaseline_LatchesOnlyWhenNothingIsLeftToPair(
		bool previouslyEstablished, bool paired, int unboundGuestCopies, bool expected) =>
		Assert.Equal(
			expected,
			EnemySpawnArbitration.ShouldRepairGenerationBaseline(previouslyEstablished, paired, unboundGuestCopies));

	[Theory]
	[InlineData(false, false, true)] // a generated copy with no host id — the repair's candidate
	[InlineData(true, false, false)] // already bound to a host id — its identity is decided (batch 20261002-f row 1)
	[InlineData(false, true, false)] // a runtime-created animal — bound through the runtime-spawn channel
	[InlineData(true, true, false)] // bound AND runtime — either reason excludes it
	public void IsRepairCandidate_OnlyTheUnboundGeneratedCopies(
		bool hasHostId, bool isRuntimeAnimal, bool expected) =>
		Assert.Equal(expected, EnemySpawnArbitration.IsRepairCandidate(hasHostId, isRuntimeAnimal));

	[Theory]
	[InlineData(85, 0, 85)] // steady state: every host fact is held, nothing was newly paired
	[InlineData(85, 85, 0)] // nothing bound yet (the pass is about to pair them)
	[InlineData(85, 3, 82)] // a partial set — the asserted count is what is actually held
	public void AssertedBoundCopies_IsTheHeldCount_NotTheNewlyPairedCount(
		int hostGeneratedFacts, int unboundGuestCopies, int expected) =>
		Assert.Equal(expected, EnemySpawnArbitration.AssertedBoundCopies(hostGeneratedFacts, unboundGuestCopies));
}
