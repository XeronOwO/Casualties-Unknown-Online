using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// <see cref="WorldService"/>'s guest report-recovery surface, split out at the
/// 600-line gate. The three channels of the family each own a table and a
/// fallback window (block state — audit gap W1, partial damage — W2, break drops
/// — W1's drop half); this partial only relays their adapter-facing calls to
/// <see cref="GuestReportRecovery"/>, which owns the state and the wire half.
/// </summary>
public sealed partial class WorldService
{
	// ---- Guest block-state report recovery (audit gap W1) ----

	public void ResetPendingBlockReports() => _messages.ResetPendingBlockReports();

	// ---- Guest partial-damage report recovery (audit gap W2) ----

	/// <summary>Guest only: record the cell's current ABSOLUTE partial damage before the live delta report goes out (the fallback's re-report source).</summary>
	public void ReportBlockDamage(int x, int y, float damage) => _messages.ReportBlockDamage(x, y, damage);

	/// <summary>Either role: the cell went air — its pending partial-damage report dies with the block.</summary>
	public void ForgetPendingBlockDamage(int x, int y) => _messages.ForgetPendingBlockDamage(x, y);

	/// <summary>Guest only: a new world/layer baseline was applied — the previous world's pending partial-damage reports are dropped.</summary>
	public void ResetPendingBlockDamageReports() => _messages.ResetPendingBlockDamageReports();

	// ---- Guest break-drop report recovery (audit gap W1's drop half) ----
	// The third channel of the same family: a break's drops are the breaker's
	// local compute and the host registers them ONLY from the report, so the
	// record/send/answer cycle mirrors the block-state channel — keyed by cell but
	// carrying the drop payload, and answered by the host's relay of that same
	// payload (never by the block-state echo, which says nothing about whether the
	// drops arrived).

	/// <summary>Guest only: record the break's drops and their reported position before its live report goes out (the fallback's re-report source).</summary>
	public void ReportBreakDrops(int x, int y, float posX, float posY, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops) =>
		_messages.GuestReports.ReportBreakDrops(x, y, posX, posY, drops, buildingDrops);

	/// <summary>Guest: the host relayed this cell's break — the drops it names are answered; a drop answered another way is forgotten individually.</summary>
	public void AnswerBreakDrops(int x, int y, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops) =>
		_messages.GuestReports.AnswerBreakDrops(x, y, drops, buildingDrops);

	/// <summary>Guest: one drop of an outstanding break left the world or was refused — stop re-reporting it.</summary>
	public void ForgetBreakDrop(ulong itemId) => _messages.GuestReports.ForgetBreakDrop(itemId);

	/// <summary>Guest: true while this side waits for the host to answer a break carrying the drop — the item reconcile must not kill it before the host knows it.</summary>
	public bool IsBreakDropPending(ulong itemId) => _messages.GuestReports.IsBreakDropPending(itemId);

	/// <summary>Guest only: a new world/layer baseline was applied — the previous world's pending break-drop reports are dropped.</summary>
	public void ResetPendingBreakDropReports() => _messages.GuestReports.ResetBreakDrops();
}
