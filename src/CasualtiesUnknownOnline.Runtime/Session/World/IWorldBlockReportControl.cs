using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The guest report-recovery surface: the three channels whose tables and fallback windows make a swallowed report heal itself — block state (audit gap W1), the
/// partial block damage (W2), and the break drops (W1's drop half).
/// </summary>
/// <remarks>
/// Declared on <see cref="BlockReportChannel"/> rather than on <see cref="IWorldControl"/> because the channel already owns that state and the wire half: a caller
/// that needs a recovery call reads <c>world.BlockReports</c> and reaches the object that keeps the table, instead of a facade forwarding to it. The world control
/// keeps the surfaces a handler needs (records, merges, snapshots); this one keeps the caller-side bookkeeping.
/// </remarks>
public interface IWorldBlockReportControl
{
	/// <summary>Guest only: drop every outstanding block-state report (a new world/layer baseline was applied).</summary>
	void ResetPendingBlockReports();

	/// <summary>
	/// Guest only: add a locally-applied hit to the cell's own contribution and return the new cumulative value for the live report (0 when the cell could not be
	/// tracked — the report then carries no contribution and the host applies its raw damage). The value is what THIS side has applied to the cell, never the cell's
	/// total: the host accounts damage per sender, so a report carrying the total would merge two senders into one number and could only ever take their maximum.
	/// Called right BEFORE the live delta report is sent; the cell stops being outstanding when the host answers for it (the cumulative value stays — the next hit
	/// reports cumulative + its increment), and dies when a block write lands on the cell or the world is replaced.
	/// </summary>
	float AddLocalBlockDamage(int x, int y, float increment);

	/// <summary>Either role: a block write landed on the cell (a break, a placement, a restored block) — the partial damage accounted for it belonged to the block that is gone, so this side's outstanding contribution and every sender's ledger entry for the cell are dropped with it.</summary>
	void ForgetBlockDamageAccounting(int x, int y);

	/// <summary>Either role: a new world/layer baseline was applied — drop every outstanding partial-damage contribution and every ledger entry from the previous world (same boundary as <see cref="ResetPendingBlockReports"/>).</summary>
	void ResetPendingBlockDamageReports();

	/// <summary>
	/// Guest only: record the break's locally-created drops (the block drops and the building-death drops the same break produced) BEFORE the live
	/// <c>BlockDamaged</c> report is sent — the fallback's re-report source. The host registers a guest's break drops exclusively from that report, so a swallowed
	/// one leaves items the authoritative table never learns about and the item keyframe cannot reconcile.
	/// </summary>
	void ReportBreakDrops(int x, int y, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops);

	/// <summary>Guest: the host relayed a break for this cell — the drops it names are answered (the accepted relay is the acknowledgement); a drop answered another way (an <c>ItemReject</c> refusal, a gone local object) is forgotten individually.</summary>
	void AnswerBreakDrops(int x, int y, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops);

	/// <summary>Guest: one drop of an outstanding break left the world or was refused — the break must not be re-reported forever for an item that no longer exists.</summary>
	void ForgetBreakDrop(ulong itemId);

	/// <summary>Guest only: true while this side still waits for the host to answer a break whose drops include <paramref name="itemId"/> — the item keyframe's reconcile must not kill that locally-created drop before the host knows it (the 5-30 s keyframe is far inside the fallback's window).</summary>
	bool IsBreakDropPending(ulong itemId);

	/// <summary>Guest: a new world/layer baseline was applied — drop every unacknowledged break-drop report from the previous world (same boundary as <see cref="ResetPendingBlockReports"/>).</summary>
	void ResetPendingBreakDropReports();
}
