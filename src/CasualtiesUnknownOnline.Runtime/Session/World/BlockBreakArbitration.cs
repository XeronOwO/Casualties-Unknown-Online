using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The block-break first-writer-wins arbitration table (PURE — no Unity, time
/// is an explicit input): a guest's air-write (BlockPlaced, SetBlock(0)) that
/// the host APPLIED proves that guest's break is the first writer for that
/// cell; the record is consumed when that guest's BlockDamaged report (the
/// drops carrier) arrives. The BlockPlaced necessarily precedes the
/// BlockDamaged (both reliable, same source), so in the NORMAL path the block is
/// ALREADY air when the drops arrive and a GetBlock check cannot tell
/// first-writer from second-writer there — this table does. The one case where
/// the block's own state LOOKS like evidence is a report that arrives while the
/// host's block still stands (no earlier break could have taken a cell this side
/// still holds); it is deliberately NOT accepted, because the record it would
/// create is layer-relative while the report is not — see
/// <see cref="TryAccept"/>. Entries without a
/// BlockDamaged (quake / environment air writes) expire. The GameAdapter's
/// BlockBreakSync feeds the game inputs (cell coordinates, Time.unscaledTime) —
/// this machine is what the tests lock.
///
/// A report may arrive AGAIN: the guest re-reports an unacknowledged break on
/// its 60 s fallback, and the host's own relay (the acknowledgement) can be the
/// lost message. A repeat from the SAME sender is therefore acknowledged
/// idempotently instead of refused — the drops it carries are already
/// registered, so re-materializing is a no-op by item id
/// (<see cref="Verdict.Repeat"/>) — while a DIFFERENT sender's report for an
/// already-broken cell stays refused (first-writer-wins). The accepted record
/// lives as long as the re-report window, so it is purged separately from the
/// short-lived unconsumed air-write records.
/// </summary>
internal sealed class BlockBreakArbitration
{
	private readonly Dictionary<(ulong Sender, int CellX, int CellY), float> _recentBroken = [];

	/// <summary>The cells a break was already accepted for: the accepting sender and when. A same-sender report inside the window is a repeat (its relay was lost); a different sender is a second breaker.</summary>
	private readonly Dictionary<(int CellX, int CellY), (ulong Sender, float Now)> _accepted = [];

	internal int Count => _recentBroken.Count;

	/// <summary>The accepted-break records (the re-report window's table) — observable for the purge test and the diagnostics path.</summary>
	internal int AcceptedCount => _accepted.Count;

	/// <summary>The host applied the sender's air-write — record it for the drops
	/// arbitration (a repeat air-write overwrites, still one break to accept).</summary>
	internal void RecordAppliedAirWrite(ulong sender, int cellX, int cellY, float now) =>
		_recentBroken[(sender, cellX, cellY)] = now;

	/// <summary>
	/// First-writer-wins: the sender's unconsumed air-write record for this cell
	/// accepts the report (and is consumed — a repeat then reads
	/// <see cref="Verdict.Repeat"/>, never a second fresh accept). Without one, a
	/// break this sender already had accepted for the cell is an idempotent
	/// REPEAT (its drops re-relay; the receiver's registration is idempotent by
	/// item id). Anything else is REFUSED: a different sender's report for an
	/// already-broken cell, or a report nobody's air write proved.
	///
	/// A report that arrives while the host's block still STANDS is refused too,
	/// deliberately: the block's own state looks like evidence of a first writer
	/// (no earlier break could have taken a cell this side still holds), but the
	/// record it would create is layer-relative and the report is not. After a
	/// descent, a stale report of the previous layer's break names a cell that now
	/// holds a freshly generated block, and accepting it on that "evidence" made
	/// the host break a block nobody had touched — with the report's REAL damage
	/// (only the fallback's re-send uses zero). Evidence for the accepting branch
	/// would have to distinguish generations, which no message carries.
	/// </summary>
	internal Verdict TryAccept(ulong sender, int cellX, int cellY)
	{
		if (_recentBroken.Remove((sender, cellX, cellY)))
		{
			return Verdict.Fresh;
		}

		return _accepted.TryGetValue((cellX, cellY), out var accepted) && accepted.Sender == sender
			? Verdict.Repeat
			: Verdict.Refused;
	}

	/// <summary>
	/// Commit an accepted break to the cell (audit gap W1's drop half). Called
	/// once the break is REAL — the sender's air write already landed
	/// (<see cref="Verdict.Fresh"/>) — never on a report that turned out not to
	/// break anything, or the cell would be attributed to a break that never
	/// happened and a genuine second breaker would be refused for the whole
	/// window. A repeat REFRESHES the entry's clock: the guest's fallback may
	/// re-report every 60 s for as long as it stays unanswered, so the window
	/// measures time since the last report rather than since the first.
	/// </summary>
	internal void RecordAccepted(ulong sender, int cellX, int cellY, float now) =>
		_accepted[(cellX, cellY)] = (sender, now);

	/// <summary>
	/// The world/layer these records describe is gone (a descent regenerates
	/// terrain; the cells are layer-relative coordinates and a session can end
	/// mid-report). Keeping an accepted cell across the boundary let a pending
	/// re-report of the OLD layer's break hit the STANDING-BLOCK verdict against a
	/// freshly generated block and break it — the record's meaning died with the
	/// layer, so it is dropped with it.
	/// </summary>
	internal void Reset()
	{
		_recentBroken.Clear();
		_accepted.Clear();
	}

	/// <summary>
	/// Remove stale records. The unconsumed air-write records use the short TTL
	/// (a break report that never arrived — quake / environment air writes, a
	/// breaker that disconnected mid-operation); the accepted-break records use
	/// the caller's longer re-report window, because the guest's fallback may
	/// re-report that break once the window elapses.
	/// </summary>
	internal void PurgeStale(float now, float ttl, float acceptedTtl)
	{
		foreach (var stale in _recentBroken.Where(kv => now - kv.Value > ttl).ToList())
		{
			_recentBroken.Remove(stale.Key);
		}

		foreach (var stale in _accepted.Where(kv => now - kv.Value.Now > acceptedTtl).ToList())
		{
			_accepted.Remove(stale.Key);
		}
	}
}
