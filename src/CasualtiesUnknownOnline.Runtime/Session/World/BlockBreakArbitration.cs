using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The block-break first-writer-wins arbitration table (PURE — no Unity, time
/// is an explicit input): a guest's air-write (BlockPlaced, SetBlock(0)) that
/// the host APPLIED proves that guest's break is the first writer for that
/// cell; the record is consumed when that guest's BlockDamaged report (the
/// drops carrier) arrives. The BlockPlaced necessarily precedes the
/// BlockDamaged (both reliable, same source), so the block is ALREADY air when
/// the drops arrive and a GetBlock check can never tell first-writer from
/// second-writer — this table does. Entries without a BlockDamaged (quake /
/// environment air writes) expire. The GameAdapter's BlockBreakSync feeds the
/// game inputs (cell coordinates, Time.unscaledTime) — this machine is what
/// the tests lock.
/// </summary>
internal sealed class BlockBreakArbitration
{
	private readonly Dictionary<(ulong Sender, int CellX, int CellY), float> _recentBroken = [];

	internal int Count => _recentBroken.Count;

	/// <summary>The host applied the sender's air-write — record it for the drops
	/// arbitration (a repeat air-write overwrites, still one break to accept).</summary>
	internal void RecordAppliedAirWrite(ulong sender, int cellX, int cellY, float now) =>
		_recentBroken[(sender, cellX, cellY)] = now;

	/// <summary>
	/// First-writer-wins: consume the sender's record for this cell — the only
	/// fact that can distinguish the first breaker from a second. One-shot per
	/// record: a second break report of the same cell is refused.
	/// </summary>
	internal bool TryAccept(ulong sender, int cellX, int cellY) =>
		_recentBroken.Remove((sender, cellX, cellY));

	/// <summary>Remove entries older than the TTL (a break report that never arrived — quake /
	/// environment air writes, a breaker that disconnected mid-operation).</summary>
	internal void PurgeStale(float now, float ttl)
	{
		foreach (var stale in _recentBroken.Where(kv => now - kv.Value > ttl).ToList())
		{
			_recentBroken.Remove(stale.Key);
		}
	}
}
