using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The GUEST's unacknowledged PARTIAL-damage bookkeeping (sync-coverage audit
/// W2), the sibling of <see cref="GuestBlockReportBookkeeping"/> (W1): the block
/// STATE and the partial DAMAGE of a cell ride different channels and have
/// different recovery units, so they keep different tables. This type owns the
/// pending table and the one-per-overflow-episode log latch; the message surface
/// (<see cref="WorldStateMessageService"/>) only sends and answers.
///
/// The rules it must keep exactly (they are what the W2 recovery is):
/// a cell is recorded with its ABSOLUTE damage before its live delta report is
/// sent; a later hit at the same cell supersedes the older value; an entry is
/// dropped when the host answers for that cell (the authoritative value its
/// answer carries) or when this side sees the cell go air; the table survives a
/// reconnect-while-in-world and is cleared only when a new world/layer baseline
/// is applied or the session ends.
/// </summary>
internal sealed class GuestBlockDamageReportBookkeeping(ILogger<WorldService> log)
{
	private readonly PendingBlockDamageTable _table = new();
	private bool _overflowLogged;

	/// <summary>How many unacknowledged reports are waiting for the host's answer (the fallback pump's work check).</summary>
	internal int Count => _table.Count;

	/// <summary>The unacknowledged cells to re-report, in table order — every entry is an independent idempotent report.</summary>
	internal IReadOnlyList<BlockDamageEntryMsg> Entries => _table.Entries;

	/// <summary>
	/// Record the cell's absolute damage BEFORE the live delta report is sent: a
	/// send that never lands is exactly what the fallback exists for. A cell the
	/// cap refuses is logged once per overflow episode, never dropped silently.
	/// </summary>
	internal void Report(int x, int y, float damage)
	{
		if (_table.Report(x, y, damage))
		{
			return;
		}

		if (_overflowLogged)
		{
			return;
		}

		_overflowLogged = true;
		log.LogWarning(
			"[BlockSync] pending partial-damage table is full ({Cap} cells) — new cells are not re-reported until the host answers (cell ({X},{Y}) dropped).",
			_table.Cap, x, y);
	}

	/// <summary>
	/// Guest: the host answered for these cells — every reported cell comes back
	/// with this host's authoritative value, so each one's pending report is done
	/// (a zero means "this host holds no damage for the cell": the local crack is
	/// not this side's to keep).
	/// </summary>
	internal void Answer(IReadOnlyList<BlockDamageEntryMsg> entries)
	{
		var dropped = 0;
		foreach (var entry in entries)
		{
			if (_table.Remove(entry.X, entry.Y))
			{
				dropped++;
			}
		}

		if (dropped == 0)
		{
			return;
		}

		if (_table.Count == 0)
		{
			_overflowLogged = false; // the overflow episode ended — a later fill must log again
		}

		log.LogDebug("[BlockSync] host answered {Dropped} partial-damage report(s) ({Remaining} left).",
			dropped, _table.Count);
	}

	/// <summary>An air write landed on the cell (local break, remote break, earthquake/environment): a broken block's damage is the block-state channel's, so the pending damage report dies with the block.</summary>
	internal void Forget(int x, int y)
	{
		if (!_table.Remove(x, y))
		{
			return;
		}

		if (_table.Count == 0)
		{
			_overflowLogged = false;
		}

		log.LogDebug("[BlockSync] cell ({X},{Y}) went air — dropped its pending partial-damage report ({Remaining} left).",
			x, y, _table.Count);
	}

	/// <summary>The world these reports belonged to is gone (a new world/layer baseline, or the session ended) — every pending report dies with it.</summary>
	internal void Reset()
	{
		if (_table.Count > 0)
		{
			log.LogInformation(
				"[BlockSync] cleared {Count} pending partial-damage report(s): they belonged to the previous world.",
				_table.Count);
			_table.Clear();
		}

		_overflowLogged = false;
	}
}
