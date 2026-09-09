using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The guest-side table of locally-applied block mutations whose report the
/// host has not answered yet: block cell → the block this side wrote there.
/// The host→guest direction heals a lost relay with the absolute block-state
/// table, but a lost guest→host report had no recovery at all (sync-coverage
/// audit W1): the host never learned the cell, so its absolute table omitted
/// it and could not heal either side. This table is that recovery's source.
/// An entry is dropped when the host answers for that cell (its relay echo or
/// its correction) or when a new world/layer baseline is applied — the
/// absolute snapshot and the world-entry marker deliberately do not clear it —
/// so the table only ever holds genuinely unacknowledged writes and stays
/// small. Bounded: at the cap a NEW cell is refused and the caller logs the
/// overflow episode once (never a silent drop; an existing cell still updates).
/// </summary>
public sealed class PendingBlockReportTable
{
	/// <summary>Same bound as the host's deviation table — a fully-mined world cannot grow the table without bound.</summary>
	public const int DefaultCap = 65536;

	private readonly Dictionary<(int X, int Y), ushort> _entries = [];
	private readonly int _cap;

	public PendingBlockReportTable()
		: this(DefaultCap)
	{
	}

	/// <summary>Test seam (InternalsVisibleTo): the cap is a policy constant; tests exercise the overflow with a small bound.</summary>
	internal PendingBlockReportTable(int cap)
	{
		_cap = cap;
	}

	public int Count => _entries.Count;

	public int Cap => _cap;

	/// <summary>
	/// Upsert the cell's pending block — a newer local write at the same cell
	/// supersedes the older report (the host only needs the current value).
	/// Returns false when the cap refused a NEW cell (an existing cell always
	/// updates).
	/// </summary>
	public bool Report(int x, int y, ushort block)
	{
		var key = (x, y);
		if (!_entries.ContainsKey(key) && _entries.Count >= _cap)
		{
			return false;
		}

		_entries[key] = block;
		return true;
	}

	/// <summary>Drop the cell — the host has answered authoritatively for it (or the world baseline was replaced). Returns whether an entry was removed.</summary>
	public bool Remove(int x, int y) => _entries.Remove((x, y));

	/// <summary>The world baseline was replaced (layer regeneration / reconnect / session end) — every pending report belongs to the previous world.</summary>
	public void Clear() => _entries.Clear();

	/// <summary>The unacknowledged cells to re-report. Every entry is an independent idempotent report, so the order is irrelevant.</summary>
	public IReadOnlyList<DamagedBlock> Entries =>
		[.. _entries.Select(entry => new DamagedBlock(entry.Key.X, entry.Key.Y, entry.Value))];
}
