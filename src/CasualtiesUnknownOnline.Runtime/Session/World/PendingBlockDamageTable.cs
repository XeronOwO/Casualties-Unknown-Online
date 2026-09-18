using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The guest-side table of locally-applied PARTIAL block damage whose absolute
/// value the host has not answered yet: block cell → the damage this side holds
/// for that cell (the game's own accumulated <c>BlockDamage.damage</c>).
///
/// The live partial-damage report is a DELTA (the receiver applies
/// <c>DamageBlock(cell, dmg, …)</c>, which accumulates), so a swallowed report
/// leaves the host's own <c>WorldGeneration.world.blockDamages</c> list short by
/// exactly that hit — and the host's absolute snapshot, which reads that list at
/// send time, then omits the cell and can never heal either side (sync-coverage
/// audit W2; the terminal break state is W1's, already landed). This table is
/// that recovery's source: the guest re-reports the cell ABSOLUTELY (replaying
/// the delta would double-apply), and the host merges it per cell.
///
/// An entry is dropped when the host answers for that cell (its authoritative
/// value, which the answer carries for every reported cell) or when this side
/// sees the cell go air — a broken block's damage is carried by the block-state
/// channel, never by a damage row. A new world/layer baseline clears the table
/// wholesale. Bounded: at the cap a NEW cell is refused and the caller logs the
/// overflow episode once (never a silent drop; an existing cell still updates).
/// </summary>
public sealed class PendingBlockDamageTable
{
	/// <summary>Same bound as the block-state pending table — a fully-damaged world cannot grow the table without bound. The live bound is much smaller: this side's own game list holds at most the game's 128 entries.</summary>
	public const int DefaultCap = 65536;

	private readonly Dictionary<(int X, int Y), float> _entries = [];
	private readonly int _cap;

	public PendingBlockDamageTable()
		: this(DefaultCap)
	{
	}

	/// <summary>Test seam (InternalsVisibleTo): the cap is a policy constant; tests exercise the overflow with a small bound.</summary>
	internal PendingBlockDamageTable(int cap)
	{
		_cap = cap;
	}

	public int Count => _entries.Count;

	public int Cap => _cap;

	/// <summary>
	/// Upsert the cell's current ABSOLUTE damage — a later local hit at the same
	/// cell supersedes the older report (the host only needs the current value).
	/// Returns false when the cap refused a NEW cell (an existing cell always
	/// updates).
	/// </summary>
	public bool Report(int x, int y, float damage)
	{
		var key = (x, y);
		if (!_entries.ContainsKey(key) && _entries.Count >= _cap)
		{
			return false;
		}

		_entries[key] = damage;
		return true;
	}

	/// <summary>Drop the cell — the host answered authoritatively for it, the cell went air, or the world baseline was replaced. Returns whether an entry was removed.</summary>
	public bool Remove(int x, int y) => _entries.Remove((x, y));

	/// <summary>The world these reports belonged to is gone (a new world/layer baseline, or the session ended) — every pending report dies with it.</summary>
	public void Clear() => _entries.Clear();

	/// <summary>The unacknowledged cells to re-report, in the wire shape the report sends. Every entry is an independent idempotent report, so the order is irrelevant.</summary>
	public IReadOnlyList<BlockDamageEntryMsg> Entries =>
		[.. _entries.Select(entry => new BlockDamageEntryMsg { X = entry.Key.X, Y = entry.Key.Y, Damage = entry.Value })];
}
