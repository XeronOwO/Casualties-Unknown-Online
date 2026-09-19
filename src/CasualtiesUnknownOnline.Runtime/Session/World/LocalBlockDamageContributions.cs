using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The GUEST's own contribution to the cells it has damaged: block cell → the
/// damage THIS side has applied to it, in the game's accumulated units
/// (<c>BlockDamage.damage</c>), plus whether the host has answered for the cell
/// since the last report.
/// <para>
/// This is the W2 recovery's source, and the reason it is a CONTRIBUTION rather
/// than the cell's absolute total: the host accounts damage per sender
/// (review/partial-damage-delta-report-overlap), so what heals a swallowed live
/// delta is "how much of this cell is mine", never "how much this cell holds on
/// my screen" — the latter merges two senders into one number and can only ever
/// take their maximum. The live report stays a delta, and every report carries
/// the cell's cumulative contribution, so a repeat is a no-op and a later hit is
/// the difference the host has not seen.
/// </para>
/// <para>
/// An entry is answered (not dropped) when the host answers for the cell: the
/// cumulative value must survive the answer, because the next hit at that cell
/// reports cumulative + its increment and the host resolves the difference
/// against the ledger it already holds. It is dropped when a block write lands
/// on the cell — the block the damage belonged to is gone, and a fresh block at
/// the same cell starts from zero — or when the world/layer baseline is
/// replaced. A reconnect-while-in-world keeps it: re-reporting is the recovery.
/// Bounded: at the cap a NEW cell is refused and the caller logs the overflow
/// episode once (never a silent drop; an existing cell still accumulates).
/// </summary>
public sealed class LocalBlockDamageContributions
{
	/// <summary>Same bound as the block-state pending table — a long mining session cannot grow the table without bound. The live bound is the number of cells this side has damaged whose blocks still stand, which the game's own list bounds too.</summary>
	public const int DefaultCap = 65536;

	private readonly Dictionary<(int X, int Y), Entry> _entries = [];
	private readonly int _cap;

	public LocalBlockDamageContributions()
		: this(DefaultCap)
	{
	}

	/// <summary>Test seam (InternalsVisibleTo): the cap is a policy constant; tests exercise the overflow with a small bound.</summary>
	internal LocalBlockDamageContributions(int cap)
	{
		_cap = cap;
	}

	/// <summary>How many cells are waiting for the host's answer (the fallback pump's work check).</summary>
	public int Count
	{
		get
		{
			var outstanding = 0;
			foreach (var entry in _entries.Values)
			{
				if (entry.Outstanding)
				{
					outstanding++;
				}
			}

			return outstanding;
		}
	}

	/// <summary>How many cells this side holds a contribution for (an answered cell keeps its value until its block is written again).</summary>
	public int TrackedCount => _entries.Count;

	public int Cap => _cap;

	/// <summary>
	/// Add a locally-applied hit's damage to the cell's contribution and mark the
	/// cell outstanding. Returns the new cumulative value, or 0 when the cap
	/// refused a NEW cell — the caller then sends no contribution at all, so the
	/// receiver falls back to applying the raw delta (bounded degradation, logged
	/// by the caller).
	/// </summary>
	public float Add(int x, int y, float increment)
	{
		var key = (x, y);
		if (!_entries.TryGetValue(key, out var entry))
		{
			if (_entries.Count >= _cap || increment <= 0f)
			{
				return 0f;
			}

			entry = new Entry();
			_entries[key] = entry;
		}

		entry.Cumulative += increment;
		entry.Outstanding = true;
		return entry.Cumulative;
	}

	/// <summary>The outstanding cells to re-report, in the wire shape the report sends — each entry is this side's cumulative contribution, so the report is idempotent on the host.</summary>
	public IReadOnlyList<BlockDamageEntryMsg> Outstanding
	{
		get
		{
			var entries = new List<BlockDamageEntryMsg>(_entries.Count);
			foreach (var pair in _entries)
			{
				if (pair.Value.Outstanding)
				{
					entries.Add(new BlockDamageEntryMsg { X = pair.Key.X, Y = pair.Key.Y, Damage = pair.Value.Cumulative });
				}
			}

			return entries;
		}
	}

	/// <summary>The host answered for the cell: it is no longer outstanding, and the cumulative value stays (the next hit reports cumulative + its own increment). Returns whether the cell was outstanding.</summary>
	public bool Answer(int x, int y)
	{
		if (!_entries.TryGetValue((x, y), out var entry) || !entry.Outstanding)
		{
			return false;
		}

		entry.Outstanding = false;
		return true;
	}

	/// <summary>A block write landed on the cell (a break, a placement, a restored block) — the contribution belonged to the block that is gone. Returns whether an entry was removed.</summary>
	public bool Forget(int x, int y) => _entries.Remove((x, y));

	/// <summary>The world these contributions belonged to is gone (a new world/layer baseline, or the session ended).</summary>
	public void Clear() => _entries.Clear();

	private sealed class Entry
	{
		internal float Cumulative { get; set; }

		internal bool Outstanding { get; set; }
	}
}
