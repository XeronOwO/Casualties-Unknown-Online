using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Enemy-spawn arbitration (PURE — no Unity; the host's facts and the member's
/// positions are explicit inputs):
/// generation-time animal entities are generated deterministically by BOTH sides
/// (<c>WorldGeneration.DistributeEntities</c>, same seed), but each side holds
/// its own process-local instances. Pairing the host's enemy instances with the
/// guest's is the precondition for syncing them — and the pairing key is the
/// generated position (deterministic). This class owns that key as well as the
/// comparison: the host's generated set enters as FACTS and is ordered by the
/// bind-time spawn anchor each fact carries, the member's entry orders its own
/// frozen copies the same way, and the two are paired index-by-index. A count
/// mismatch or an out-of-tolerance pair is a generation divergence — reported
/// with the index it diverged at, never silently mispaired.
/// </summary>
internal sealed class EnemySpawnArbitration
{
	/// <summary>Max allowed distance between a paired host/guest spawn position (world units) — only absorbs float jitter, not a real divergence.</summary>
	internal const float PairTolerance = 0.5f;

	/// <summary>The deterministic allocation/pairing key: (x, y) ascending. Both sides' generated positions are identical, so both orders are identical.</summary>
	internal static int Compare(NetVector2 a, NetVector2 b)
	{
		var byX = a.X.CompareTo(b.X);
		return byX != 0 ? byX : a.Y.CompareTo(b.Y);
	}

	/// <summary>
	/// Pair the host's generated facts against one member's own copies — the
	/// generated-set pairing of a world entry and of the 60 s in-session repair.
	/// <para>
	/// The host side enters as FACTS, never as a position list, because the key
	/// is the anchor each fact carries and nothing else. The live
	/// <see cref="EnemyEntity.Position"/> is where the host's simulation has
	/// already driven that animal, so a pairing keyed on it holds only in the
	/// instant after generation. Batch `20261006-g` read what letting the caller
	/// pick the field costs: a member that entered the world two minutes into a
	/// run sorted the host's facts by their anchors and then compared the host's
	/// LIVE positions against its own by-then-frozen copies, so the all-or-nothing
	/// set failed on every 60 s repair for the whole session — five consecutive
	/// cycles of `69 host vs 69 guest`, `mapping=False`, its generated animals
	/// never following the host — while the member present at world entry, whose
	/// one attempt ran a second after generation, read `mapping=True`.
	/// </para>
	/// <para>
	/// Both sides are ordered by <see cref="Compare"/> first, then paired
	/// index-by-index: the whole set pairs or none of it does (see
	/// <see cref="PairingDivergence"/> for the reading a refusal reports).
	/// </para>
	/// </summary>
	/// <param name="hostGeneratedFacts">The host's generated (non-runtime) enemy facts, in any order.</param>
	/// <param name="copyPositions">The member's unbound copies, in the caller's own order — they are frozen at their generation positions, so each one's position IS its spawn position.</param>
	/// <param name="pairs">The paired facts with the index of the member's copy each one binds, in the host's anchor order; empty when the verdict is false.</param>
	/// <param name="divergence">What the attempt concluded: both counts, and on a key mismatch the first index whose distance exceeded <see cref="PairTolerance"/> with that distance.</param>
	internal static bool TryPairGeneratedCopies(
		IReadOnlyList<EnemyEntity> hostGeneratedFacts,
		IReadOnlyList<NetVector2> copyPositions,
		out IReadOnlyList<(EnemyEntity Host, int CopyIndex, float Distance)> pairs,
		out PairingDivergence divergence)
	{
		pairs = [];
		var comparer = Comparer<NetVector2>.Create(Compare);
		var hostOrdered = hostGeneratedFacts.OrderBy(fact => fact.SpawnPosition, comparer).ToList();
		// The copy INDICES in anchor order — the caller binds by its own list. A
		// stable order is what keeps two copies at the SAME position mapping to the
		// same pair as the host side, whose LINQ order is stable too.
		var copyOrder = Enumerable.Range(0, copyPositions.Count)
			.OrderBy(index => copyPositions[index], comparer)
			.ToList();

		if (hostOrdered.Count != copyOrder.Count)
		{
			divergence = new PairingDivergence(hostOrdered.Count, copyPositions.Count, GeneratedPairingOutcome.CountMismatch, -1, 0f);
			return false;
		}

		var result = new List<(EnemyEntity, int, float)>(hostOrdered.Count);
		for (var i = 0; i < hostOrdered.Count; i++)
		{
			var distance = Distance(hostOrdered[i].SpawnPosition, copyPositions[copyOrder[i]]);
			if (distance > PairTolerance)
			{
				divergence = new PairingDivergence(hostOrdered.Count, copyPositions.Count, GeneratedPairingOutcome.KeyMismatch, i, distance);
				return false;
			}

			result.Add((hostOrdered[i], copyOrder[i], distance));
		}

		pairs = result;
		divergence = new PairingDivergence(hostOrdered.Count, copyPositions.Count, GeneratedPairingOutcome.Paired, -1, 0f);
		return true;
	}

	internal static float Distance(NetVector2 a, NetVector2 b)
	{
		var dx = a.X - b.X;
		var dy = a.Y - b.Y;
		return (float)Math.Sqrt(dx * dx + dy * dy);
	}

	/// <summary>
	/// Is the generation baseline established after a repair pass?
	/// <paramref name="paired"/> is what <see cref="TryPairGeneratedCopies"/>
	/// returned this cycle, or the false sentinel when the pass never attempted a
	/// pair (no candidates); <paramref name="unboundGuestCopies"/> counts the
	/// local copies that still have no host id. A pair success establishes it.
	/// Zero copies left to pair means the mapping object did not change — the
	/// baseline this member established earlier stays established, so a repair
	/// with nothing to do PRESERVES the flag instead of clearing it; clearing it
	/// used to switch off the runtime-spawn bind as collateral (batch
	/// `20261002-f` row 1, where already-bound copies were re-paired and every
	/// cycle reported mapping=False). Anything else — copies that still need
	/// pairing and did not pair — is a generation divergence: not established.
	/// </summary>
	internal static bool ShouldRepairGenerationBaseline(bool previouslyEstablished, bool paired, int unboundGuestCopies) =>
		paired || (previouslyEstablished && unboundGuestCopies == 0);

	/// <summary>
	/// Is this local animal a candidate for the generation-baseline repair?
	/// A copy that already carries a host id is NOT: its identity is decided, and
	/// re-pairing it compares the host's bind-time anchor against wherever the
	/// 20 Hz drive has moved it — the all-or-nothing set then fails on every 60 s
	/// repair (batch `20261002-f` row 1, 4/4 cycles). A runtime-created animal is
	/// bound through the runtime-spawn channel, never through the generation
	/// baseline. So the candidates are exactly the generated copies with no id yet.
	/// </summary>
	internal static bool IsRepairCandidate(bool hasHostId, bool isRuntimeAnimal) =>
		!hasHostId && !isRuntimeAnimal;

	/// <summary>
	/// The generated copies this member's repair pass can assert as bound: the
	/// host's generated facts minus the local candidates that still have no id.
	/// Reported on every applied snapshot so a steady-state repair (nothing left
	/// to pair) still reads as `N generated bound` — the number that verifies the
	/// baseline, not the number newly paired this cycle. WITHOUT it a healthy
	/// repeat prints `0 generated bound`, indistinguishable from the rejected
	/// `0 generated bound, … mapping=False` line except by the mapping field
	/// (review finding, batch `20261002-f` re-run).
	/// </summary>
	internal static int AssertedBoundCopies(int hostGeneratedFacts, int unboundGuestCopies) =>
		hostGeneratedFacts - unboundGuestCopies;

	/// <summary>What a generated-set pairing attempt concluded — the three answers the caller branches on, stated rather than inferred from a sentinel.</summary>
	internal enum GeneratedPairingOutcome
	{
		/// <summary>The whole set paired index-by-index inside <see cref="PairTolerance"/>.</summary>
		Paired,

		/// <summary>The two sides hold a different number of enemies, so no index was compared.</summary>
		CountMismatch,

		/// <summary>The counts agree and one index exceeded <see cref="PairTolerance"/> — the whole set is refused.</summary>
		KeyMismatch,
	}

	/// <summary>
	/// Where one generated-set pairing attempt diverged: both sides' counts and,
	/// on a key mismatch, the first index whose distance exceeded
	/// <see cref="PairTolerance"/> — as an index into the HOST's anchor order
	/// (the two orders are the same list by then, so it also names the member's
	/// copy at that rank). A count mismatch and "the copies stand somewhere else"
	/// are different divergences, and batch `20261006-g` could read only the
	/// counts: the index and its distance are what separate a member whose
	/// generation stream did not reproduce the host's from one the pairing key
	/// itself misjudged. <see cref="Outcome"/> carries which of the three answers
	/// this is, so a SUCCESS is not read as a count mismatch — the sentinel index
	/// alone cannot say that, and the caller reads this record after both verdicts.
	/// </summary>
	internal readonly record struct PairingDivergence(
		int HostCount,
		int CopyCount,
		GeneratedPairingOutcome Outcome,
		int FirstMismatchIndex,
		float FirstMismatchDistance)
	{
		/// <summary>True only when the two sides' counts differ — no index was compared, so there is no distance to report.</summary>
		internal bool IsCountMismatch => Outcome == GeneratedPairingOutcome.CountMismatch;
	}
}
