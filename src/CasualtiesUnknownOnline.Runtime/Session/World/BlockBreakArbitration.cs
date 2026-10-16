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
/// the block's own state looks like evidence is a report that arrives while the
/// host's block still stands; it is accepted ONLY when the report's generation
/// stamp proves it is this generation's (the lost-air-write shape), because the
/// record it creates is layer-relative and without the stamp a previous layer's
/// report is indistinguishable from a legitimate one — see <see cref="TryAccept"/>. Entries without a
/// BlockDamaged (quake / environment air writes) expire. The GameAdapter's
/// BlockBreakSync feeds the game inputs (cell coordinates, Time.unscaledTime) —
/// this machine is what the tests lock.
///
/// A report may arrive AGAIN: the guest re-reports an unacknowledged break on
/// its own fallback window (5 s inside the guest's own 60 s entry phase, then
/// once a minute), and the host's own relay (the acknowledgement) can be the
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
	private readonly Dictionary<(ulong Sender, int CellX, int CellY), (float Now, ushort DestroyedBlock, IReadOnlyList<DropBound> BuildingRows)> _recentBroken = [];

	/// <summary>The cells a break was already accepted for: the accepting sender, when, which block that break destroyed, and what the buildings losing support there could drop. A same-sender report inside the window is a repeat (its relay was lost); a different sender is a second breaker.</summary>
	private readonly Dictionary<(int CellX, int CellY), (ulong Sender, float Now, ushort DestroyedBlock, IReadOnlyList<DropBound> BuildingRows)> _accepted = [];

	internal int Count => _recentBroken.Count;

	/// <summary>The accepted-break records (the re-report window's table) — observable for the purge test and the diagnostics path.</summary>
	internal int AcceptedCount => _accepted.Count;

	/// <summary>
	/// The host applied the sender's air-write — record it for the drops
	/// arbitration (a repeat air-write overwrites, still one break to accept).
	/// <paramref name="destroyedBlock"/> is the block the host's OWN world held at
	/// that cell when the write landed (read before the write was applied), and it
	/// is what the drops report is later bounded against: the cell is air by then,
	/// so this record is the only surviving statement of what was destroyed — and
	/// it is the host's own read, never the reporter's claim. The parameter is
	/// REQUIRED: 0 is a real block id (air), which the drop table reads as
	/// "produces nothing", so a default here would turn a caller that forgot it
	/// into a host that refuses every drop in silence.
	/// <para>
	/// <paramref name="buildingRows"/> is the same fact one level up: the drop
	/// tables of the buildings that lost their support block in THIS break, read
	/// off the live entities by the caller at the only moment they still exist.
	/// It rides this record for the same reason <paramref name="destroyedBlock"/>
	/// does — the judgement happens after they are gone — and it is required for
	/// the same reason, so a caller that forgets it refuses the report's building
	/// drops instead of judging them.
	/// </para>
	/// </summary>
	internal void RecordAppliedAirWrite(ulong sender, int cellX, int cellY, float now, ushort destroyedBlock, IReadOnlyList<DropBound> buildingRows) =>
		_recentBroken[(sender, cellX, cellY)] = (now, destroyedBlock, buildingRows);

	/// <summary>
	/// First-writer-wins: the sender's unconsumed air-write record for this cell
	/// accepts the report (and is consumed — a repeat then reads
	/// <see cref="Verdict.Repeat"/>, never a second fresh accept). Without one, a
	/// break this sender already had accepted for the cell is an idempotent
	/// REPEAT (its drops re-relay; the receiver's registration is idempotent by
	/// item id). Anything else is REFUSED — a different sender's report for an
	/// already-broken cell, or a report nobody's air write proved — with ONE
	/// exception the generation stamp makes attributable:
	/// <see cref="Verdict.LostAirWrite"/>.
	///
	/// <paramref name="generationVerified"/> is the caller's comparison of the
	/// report's world/layer stamp with this side's own generation, and
	/// <paramref name="cellIsAir"/> is the host's live cell state. A verified
	/// report naming a cell that still STANDS is the lost-air-write shape: the
	/// guest broke the block, its air-write report was lost together with the
	/// drops-carrying one, so no record exists and the cell is untouched here.
	/// Before the stamp existed this shape was refused — the record it creates is
	/// layer-relative and the report was not, so after a descent a stale previous
	/// layer's report read as exactly this shape, and the host applied it as a
	/// break of a freshly generated block with that report's REAL damage (only a fallback's re-send carries zero). The
	/// stamp removes that ambiguity, so the legitimate case is accepted and a
	/// stale report is refused before it reaches this machine.
	///
	/// The verified clause is scoped to the UNRECORDED shape on purpose: a report
	/// whose air write IS recorded but whose cell stands again (someone rebuilt
	/// in the record's short window) stays refused, and a different sender on an
	/// accepted cell stays refused whatever the cell looks like — first-writer-wins
	/// is unchanged.
	///
	/// <paramref name="destroyedBlock"/> answers what the accepted break destroyed,
	/// on every accepting verdict — the host's own read before its air write was
	/// applied (Fresh), the block the running record was created with (Repeat), or
	/// this side's live cell for the standing shape (LostAirWrite). It is 0 on a
	/// refusal, where nothing gets judged against it.
	///
	/// <paramref name="buildingRows"/> answers the same question for the buildings
	/// that lost their support block in that break, and its EMPTY value is not
	/// "nothing can drop" but "this machine holds no answer": on the standing shape
	/// no air-write record exists to have captured one, and the caller — which runs
	/// before the entities are destroyed — is the side that supplies it. A refusal
	/// leaves it empty too.
	/// </summary>
	internal Verdict TryAccept(ulong sender, int cellX, int cellY, bool cellIsAir, bool generationVerified, ushort standingBlock, out ushort destroyedBlock, out IReadOnlyList<DropBound> buildingRows)
	{
		if (_recentBroken.TryGetValue((sender, cellX, cellY), out var recorded))
		{
			// Consumed: TryGetValue + Remove rather than Remove(key, out value) —
			// that overload is netstandard-2.1, and this tree targets net48.
			_recentBroken.Remove((sender, cellX, cellY));
			destroyedBlock = recorded.DestroyedBlock;
			buildingRows = recorded.BuildingRows;
			return Verdict.Fresh;
		}

		if (_accepted.TryGetValue((cellX, cellY), out var accepted))
		{
			destroyedBlock = accepted.DestroyedBlock;
			buildingRows = accepted.BuildingRows;
			return accepted.Sender == sender ? Verdict.Repeat : Verdict.Refused;
		}

		if (generationVerified && !cellIsAir)
		{
			destroyedBlock = standingBlock;
			buildingRows = NoBuildingRows;
			return Verdict.LostAirWrite;
		}

		destroyedBlock = 0;
		buildingRows = NoBuildingRows;
		return Verdict.Refused;
	}

	/// <summary>
	/// Commit an accepted break to the cell (audit gap W1's drop half). Called
	/// once the break is REAL — the sender's air write already landed
	/// (<see cref="Verdict.Fresh"/>) — never on a report that turned out not to
	/// break anything, or the cell would be attributed to a break that never
	/// happened and a genuine second breaker would be refused for the whole
	/// window. A repeat REFRESHES the entry's clock: the guest's fallback may
	/// re-report for as long as it stays unanswered, so the window
	/// measures time since the last report rather than since the first.
	/// <paramref name="destroyedBlock"/> is carried with the record for the same
	/// reason the air-write record carries it: a repeat arrives with the SAME
	/// drop set, and bounding it needs the block that break destroyed — the cell
	/// itself stopped saying so the moment the break landed.
	/// <paramref name="buildingRows"/> is carried for exactly that reason too, and
	/// it is the one that makes a repeat judge its building drops EXACTLY as the
	/// first report did: the entities are long destroyed by then, so a repeat that
	/// re-read the world would find nothing and refuse drops this host already
	/// registered and relayed.
	/// </summary>
	internal void RecordAccepted(ulong sender, int cellX, int cellY, float now, ushort destroyedBlock, IReadOnlyList<DropBound> buildingRows) =>
		_accepted[(cellX, cellY)] = (sender, now, destroyedBlock, buildingRows);

	/// <summary>The answer this machine has when it captured nothing: no building's rows, which callers read as "no table here" rather than "a table of no rows".</summary>
	private static readonly IReadOnlyList<DropBound> NoBuildingRows = [];

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
		foreach (var stale in _recentBroken.Where(kv => now - kv.Value.Now > ttl).ToList())
		{
			_recentBroken.Remove(stale.Key);
		}

		foreach (var stale in _accepted.Where(kv => now - kv.Value.Now > acceptedTtl).ToList())
		{
			_accepted.Remove(stale.Key);
		}
	}
}
