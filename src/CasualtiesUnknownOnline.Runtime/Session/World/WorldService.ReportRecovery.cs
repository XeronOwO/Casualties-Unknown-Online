using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// <see cref="WorldService"/>'s guest report-recovery surface, split out at the
/// 600-line gate. The three channels of the family each own a table and a
/// fallback window (block state — audit gap W1, partial damage — W2, break drops
/// — W1's drop half); this partial only relays their adapter-facing calls to
/// <see cref="BlockReportChannel"/>, which owns the state and the wire half.
/// </summary>
public sealed partial class WorldService
{
	// ---- Guest block-state report recovery (audit gap W1) ----

	public void ResetPendingBlockReports() => _blockReports.ResetPendingBlockReports();

	// ---- Guest partial-damage report recovery (audit gap W2) ----

	/// <summary>Guest only: add a locally-applied hit to the cell's own contribution and return the new cumulative value for the live report (0 = not tracked, so the report carries no contribution).</summary>
	public float AddLocalBlockDamage(int x, int y, float increment) => _blockReports.AddLocalBlockDamage(x, y, increment);

	/// <summary>Either role: a block write landed on the cell — this side's outstanding contribution and every sender's ledger entry for it die with the block.</summary>
	public void ForgetBlockDamageAccounting(int x, int y) => _blockReports.ForgetBlockDamageAccounting(x, y);

	/// <summary>Either role: a new world/layer baseline was applied — the previous world's outstanding contributions and ledger entries are dropped.</summary>
	public void ResetPendingBlockDamageReports() => _blockReports.ResetPendingBlockDamageReports();

	// ---- Guest break-drop report recovery (audit gap W1's drop half) ----
	// The third channel of the same family: a break's drops are the breaker's
	// local compute and the host registers them ONLY from the report, so the
	// record/send/answer cycle mirrors the block-state channel — keyed by cell but
	// carrying the drop payload, and answered by the host's relay of that same
	// payload (never by the block-state echo, which says nothing about whether the
	// drops arrived).

	/// <summary>Guest only: record the break's locally-created drops before its live report goes out (the fallback's re-report source) — the break message is cell-keyed, so no world position is part of the record.</summary>
	public void ReportBreakDrops(int x, int y, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops) =>
		_blockReports.GuestReports.ReportBreakDrops(x, y, drops, buildingDrops);

	/// <summary>Guest: the host relayed this cell's break — the drops it names are answered; a drop answered another way is forgotten individually.</summary>
	public void AnswerBreakDrops(int x, int y, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops) =>
		_blockReports.GuestReports.AnswerBreakDrops(x, y, drops, buildingDrops);

	/// <summary>Guest: one drop of an outstanding break left the world or was refused — stop re-reporting it.</summary>
	public void ForgetBreakDrop(ulong itemId) => _blockReports.GuestReports.ForgetBreakDrop(itemId);

	/// <summary>Guest: true while this side waits for the host to answer a break carrying the drop — the item reconcile must not kill it before the host knows it.</summary>
	public bool IsBreakDropPending(ulong itemId) => _blockReports.GuestReports.IsBreakDropPending(itemId);

	/// <summary>Guest only: a new world/layer baseline was applied — the previous world's pending break-drop reports are dropped.</summary>
	public void ResetPendingBreakDropReports() => _blockReports.GuestReports.ResetBreakDrops();
}
