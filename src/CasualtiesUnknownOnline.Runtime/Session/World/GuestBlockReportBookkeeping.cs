using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The GUEST's unacknowledged block-report bookkeeping (sync-coverage audit W1),
/// split out of <see cref="WorldStateMessageService"/> so the message surface
/// keeps the wire and this type owns the recovery state: the pending table and
/// the one-per-overflow-episode log latch.
///
/// The rules it must keep exactly (they are what the W1 recovery is):
/// a cell is recorded BEFORE its live report is sent; a newer write at the same
/// cell supersedes the older report; an entry is dropped when the host answers
/// for that cell (its relay echo or its correction); the table survives a
/// reconnect-while-in-world and is cleared only when a new world/layer baseline
/// is applied or the session ends.
/// </summary>
internal sealed class GuestBlockReportBookkeeping(ILogger<WorldService> log)
{
	private readonly PendingBlockReportTable _table = new();
	private bool _overflowLogged;

	/// <summary>How many unacknowledged reports are waiting for the host's answer (the fallback pump's work check).</summary>
	internal int Count => _table.Count;

	/// <summary>The unacknowledged cells to re-report, in table order — every entry is an independent idempotent report.</summary>
	internal IReadOnlyList<DamagedBlock> Entries => _table.Entries;

	/// <summary>
	/// Record the cell as unacknowledged BEFORE the live report is sent: a send
	/// that never lands is exactly what the fallback exists for. A cell the cap
	/// refuses is logged once per overflow episode, never dropped silently.
	/// </summary>
	internal void Report(int x, int y, ushort block)
	{
		if (_table.Report(x, y, block))
		{
			return;
		}

		if (_overflowLogged)
		{
			return;
		}

		_overflowLogged = true;
		log.LogWarning(
			"[BlockSync] pending block-report table is full ({Cap} cells) — new cells are not re-reported until the host answers (cell ({X},{Y}) dropped).",
			_table.Cap, x, y);
	}

	/// <summary>Guest: the host answered for this cell (its relay echo or its correction) — the pending report is done.</summary>
	internal void Answer(int x, int y)
	{
		if (!_table.Remove(x, y))
		{
			return;
		}

		if (_table.Count == 0)
		{
			_overflowLogged = false; // the overflow episode ended — a later fill must log again
		}

		log.LogDebug("[BlockSync] host answered ({X},{Y}) — dropped the pending report ({Remaining} left).",
			x, y, _table.Count);
	}

	/// <summary>The world these reports belonged to is gone (a new world/layer baseline, or the session ended) — every pending report dies with it.</summary>
	internal void Reset()
	{
		if (_table.Count > 0)
		{
			log.LogInformation(
				"[BlockSync] cleared {Count} pending block report(s): they belonged to the previous world.",
				_table.Count);
			_table.Clear();
		}

		_overflowLogged = false;
	}
}
