using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The GUEST's partial-damage contribution bookkeeping (sync-coverage audit W2,
/// accounted per sender since review/partial-damage-delta-report-overlap), the
/// sibling of <see cref="GuestBlockReportBookkeeping"/> (W1): the block STATE and
/// the partial DAMAGE of a cell ride different channels and have different
/// recovery units, so they keep different tables. This type owns the
/// contribution table and the one-per-overflow-episode log latch; the message
/// surface (<see cref="BlockReportChannel"/>) only sends, answers and reads the
/// cumulative value it puts on the wire.
///
/// The rules it must keep exactly (they are what the W2 recovery is, now with a
/// contribution instead of a total): a locally-applied hit is added to the cell's
/// own cumulative value BEFORE its live delta report goes out, and the value goes
/// out with that report; a repeat of the same value is a no-op on the host, which
/// resolves it against the ledger it holds; the entry stops being outstanding
/// when the host answers for that cell (its value STAYS — the next hit reports
/// cumulative + increment); it is dropped when a block write lands on the cell or
/// when a new world/layer baseline is applied; a reconnect-while-in-world keeps
/// it, because re-reporting is the recovery.
/// </summary>
internal sealed class GuestBlockDamageReportBookkeeping(ILogger<WorldService> log)
{
	private readonly LocalBlockDamageContributions _table = new();
	private bool _overflowLogged;

	/// <summary>How many cells are waiting for the host's answer (the fallback pump's work check).</summary>
	internal int Count => _table.Count;

	/// <summary>The outstanding cells to re-report, in table order — each entry is this side's cumulative contribution, so the report is idempotent on the host.</summary>
	internal IReadOnlyList<BlockDamageEntryMsg> Outstanding => _table.Outstanding;

	/// <summary>
	/// Add a locally-applied hit to the cell's contribution BEFORE the live delta
	/// report is sent (a send that never lands is exactly what the fallback exists
	/// for) and return the new cumulative value. A cell the cap refuses is logged
	/// once per overflow episode, never dropped silently — the caller then sends
	/// no contribution for it, so the host falls back to the raw delta.
	/// </summary>
	internal float Add(int x, int y, float increment)
	{
		var cumulative = _table.Add(x, y, increment);
		if (cumulative <= 0f && increment > 0f && !_overflowLogged)
		{
			_overflowLogged = true;
			log.LogWarning(
				"[BlockSync] partial-damage contribution table is full ({Cap} cells) — ({X},{Y}) is not accounted for and its live report carries no contribution until the block is written again.",
				_table.Cap, x, y);
		}

		return cumulative;
	}

	/// <summary>
	/// Guest: the host answered for these cells — every reported cell comes back
	/// (this host's value for it, 0 when it holds none), so each one's report is
	/// accounted for. The cumulative value is KEPT: the next hit at that cell
	/// reports cumulative + its increment, and the host resolves the difference.
	/// </summary>
	internal void Answer(IReadOnlyList<BlockDamageEntryMsg> entries)
	{
		var answered = 0;
		foreach (var entry in entries)
		{
			if (_table.Answer(entry.X, entry.Y))
			{
				answered++;
			}
		}

		if (answered == 0)
		{
			return;
		}

		if (_table.Count == 0)
		{
			_overflowLogged = false; // the overflow episode ended — a later fill must log again
		}

		log.LogDebug("[BlockSync] host answered {Answered} partial-damage contribution(s) ({Remaining} left).",
			answered, _table.Count);
	}

	/// <summary>A block write landed on the cell (a break, a placement, a restored block): the contribution belonged to the block that is gone, so it dies with it.</summary>
	internal void Forget(int x, int y)
	{
		if (!_table.Forget(x, y))
		{
			return;
		}

		if (_table.Count == 0)
		{
			_overflowLogged = false;
		}

		log.LogDebug("[BlockSync] cell ({X},{Y}) was written — dropped its partial-damage contribution ({Remaining} still outstanding).",
			x, y, _table.Count);
	}

	/// <summary>The world these contributions belonged to is gone (a new world/layer baseline, or the session ended) — every entry dies with it.</summary>
	internal void Reset()
	{
		if (_table.TrackedCount > 0)
		{
			log.LogInformation(
				"[BlockSync] cleared {Count} partial-damage contribution(s): they belonged to the previous world.",
				_table.TrackedCount);
			_table.Clear();
		}

		_overflowLogged = false;
	}
}
