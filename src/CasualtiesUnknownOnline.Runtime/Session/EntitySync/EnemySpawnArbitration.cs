using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Enemy-spawn arbitration (PURE — no Unity, positions are explicit inputs):
/// generation-time animal entities are generated deterministically by BOTH sides
/// (<c>WorldGeneration.DistributeEntities</c>, same seed), but each side holds
/// its own process-local instances. Pairing the host's enemy instances with the
/// guest's is the precondition for syncing them — and the pairing key is the
/// generated position (deterministic). The host orders its animal entities by
/// (x, y) ascending and assigns ids in that order; the guest orders its own the
/// same way and pairs index-by-index. A count mismatch or an out-of-tolerance
/// pair is a generation divergence — reported, never silently mispaired.
/// </summary>
internal sealed class EnemySpawnArbitration
{
	/// <summary>Max allowed distance between a paired host/guest spawn position (world units) — only absorbs float jitter, not a real divergence.</summary>
	internal const float PairTolerance = 0.5f;

	/// <summary>The deterministic allocation/pairing key: (x, y) ascending. Both sides' generated positions are identical, so both orders are identical. The Game Adapter sorts its animal entities with this same key.</summary>
	internal static int Compare(NetVector2 a, NetVector2 b)
	{
		var byX = a.X.CompareTo(b.X);
		return byX != 0 ? byX : a.Y.CompareTo(b.Y);
	}

	/// <summary>Deterministic allocation order: (x, y) ascending (see <see cref="Compare"/>).</summary>
	internal static IReadOnlyList<NetVector2> Order(IEnumerable<NetVector2> positions)
	{
		var ordered = positions.ToList();
		ordered.Sort(Compare);
		return ordered;
	}

	/// <summary>
	/// Pair the host's ordered spawn positions with the guest's ordered spawn
	/// positions index-by-index. Returns false (and no pairs) when the counts
	/// differ or any pair exceeds <see cref="PairTolerance"/> — the caller must
	/// treat that as a generation divergence (warn + degrade), not pair anyway.
	/// </summary>
	internal static bool TryPair(
		IReadOnlyList<NetVector2> hostOrdered,
		IReadOnlyList<NetVector2> guestOrdered,
		out IReadOnlyList<(int HostIndex, int GuestIndex, float Distance)> pairs)
	{
		pairs = [];
		if (hostOrdered.Count != guestOrdered.Count)
		{
			return false;
		}

		var result = new List<(int, int, float)>(hostOrdered.Count);
		for (var i = 0; i < hostOrdered.Count; i++)
		{
			var distance = Distance(hostOrdered[i], guestOrdered[i]);
			if (distance > PairTolerance)
			{
				return false;
			}

			result.Add((i, i, distance));
		}

		pairs = result;
		return true;
	}

	internal static float Distance(NetVector2 a, NetVector2 b)
	{
		var dx = a.X - b.X;
		var dy = a.Y - b.Y;
		return (float)Math.Sqrt(dx * dx + dy * dy);
	}
}
