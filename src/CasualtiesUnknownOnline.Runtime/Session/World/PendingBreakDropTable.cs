using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The guest-side table of locally-created BLOCK-BREAK drops whose report the
/// host has not answered yet: block cell → the drops that break produced. A
/// guest's break travels as two messages (the air write — W1 — and, one frame
/// later, one <c>BlockDamaged</c> carrying the break plus every drop), and the
/// guest registers its drops nowhere: the host learns them exclusively from that
/// second message. A swallowed report therefore leaves the breaker's locally
/// created items unknown to the authoritative table, and the item keyframe has
/// no fact to reconcile them from — the divergence that cannot heal (sync-
/// coverage audit W1's drop half; the block STATE half is already landed).
///
/// This table is that recovery's source. It is keyed by CELL like its two
/// siblings (<see cref="PendingBlockReportTable"/>, <see cref="PendingBlockDamageTable"/>)
/// but carries the drop payload, because the recovery unit is the break's drop
/// set and the acknowledgement is the host's relay of that same set — a
/// cell-only key could not tell a re-report of THIS break from a new one.
/// An entry is dropped when the host relays the break carrying its drops (the
/// accepted relay's echo — the W1 answer shape) or when a new world/layer
/// baseline is applied; the duplicate guard is the drop's item id, never the
/// cell alone, so a re-report can never double-register (the receiving table's
/// registration is already idempotent per item id).
///
/// Bounded: at the cap a NEW cell is refused and the caller logs the overflow
/// episode once (never a silent drop; an existing cell still updates).
/// </summary>
public sealed class PendingBreakDropTable
{
	/// <summary>Same bound as the two sibling pending tables — a long mining session cannot grow the table without bound.</summary>
	public const int DefaultCap = 65536;

	private readonly Dictionary<(int X, int Y), Entry> _entries = [];
	private readonly int _cap;

	public PendingBreakDropTable()
		: this(DefaultCap)
	{
	}

	/// <summary>Test seam (InternalsVisibleTo): the cap is a policy constant; tests exercise the overflow with a small bound.</summary>
	internal PendingBreakDropTable(int cap)
	{
		_cap = cap;
	}

	public int Count => _entries.Count;

	public int Cap => _cap;

	/// <summary>
	/// Record the break's drops at its cell — called right BEFORE the live
	/// <c>BlockDamaged</c> report goes out, because a send that never lands is
	/// exactly what the fallback exists for. A second local break at the same
	/// cell APPENDS to the outstanding set: its drops are still real items on
	/// this side, so overwriting them would lose them for good. Returns false
	/// when the cap refused a NEW cell (an existing cell always updates).
	/// </summary>
	public bool Report(int x, int y, float posX, float posY, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops)
	{
		var key = (x, y);
		if (!_entries.TryGetValue(key, out var entry))
		{
			if (_entries.Count >= _cap)
			{
				return false;
			}

			entry = new Entry(posX, posY);
			_entries[key] = entry;
		}

		if (drops is not null)
		{
			entry.Drops.AddRange(drops);
		}

		if (buildingDrops is not null)
		{
			entry.BuildingDrops.AddRange(buildingDrops);
		}

		return true;
	}

	/// <summary>
	/// The host relayed a break for this cell — its payload is the acknowledgement
	/// of exactly the drops it carries: when every one of the entry's outstanding
	/// item ids is named by the relay, the report converged and the entry is done.
	/// Returns whether an entry was removed.
	/// </summary>
	public bool Answer(int x, int y, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops)
	{
		if (!_entries.TryGetValue((x, y), out var entry) || !Covers(entry, drops, buildingDrops))
		{
			return false;
		}

		return _entries.Remove((x, y));
	}

	/// <summary>
	/// A single drop of an outstanding set was answered another way (the host
	/// refused it — <c>ItemReject</c> — or the local object is gone): drop just
	/// that item so the break is not re-reported forever for a drop that no
	/// longer exists. An entry whose set empties is removed.
	/// </summary>
	public bool ForgetItem(ulong itemId)
	{
		foreach (var pair in _entries)
		{
			if (!pair.Value.RemoveItem(itemId))
			{
				continue;
			}

			if (pair.Value.IsEmpty)
			{
				_entries.Remove(pair.Key);
			}

			return true;
		}

		return false;
	}

	/// <summary>The world these reports belonged to is gone (a new world/layer baseline, or the session ended) — every pending report dies with it.</summary>
	public void Clear() => _entries.Clear();

	/// <summary>True while <paramref name="itemId"/> is one of the drops an unacknowledged break is waiting to have answered — the item keyframe's reconcile must leave that locally-created drop alone.</summary>
	public bool IsPending(ulong itemId)
	{
		foreach (var entry in _entries.Values)
		{
			if (entry.ContainsItem(itemId))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>The unacknowledged drop sets to re-report, in table order — every entry is an independent idempotent report.</summary>
	public IReadOnlyList<PendingBreakDrops> Entries
	{
		get
		{
			var entries = new List<PendingBreakDrops>(_entries.Count);
			foreach (var pair in _entries)
			{
				entries.Add(new PendingBreakDrops(pair.Key.X, pair.Key.Y, pair.Value.PosX, pair.Value.PosY, pair.Value.Drops, pair.Value.BuildingDrops));
			}

			return entries;
		}
	}

	/// <summary>True when the relay carries every drop this entry is waiting for — an empty family is trivially covered (a break with only building drops is acknowledged by a relay whose block-drop list is empty).</summary>
	private static bool Covers(Entry entry, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops)
	{
		foreach (var outstanding in entry.Drops)
		{
			if (drops is null || !Contains(drops, outstanding.ItemId))
			{
				return false;
			}
		}

		foreach (var outstanding in entry.BuildingDrops)
		{
			if (buildingDrops is null || !Contains(buildingDrops, outstanding.ItemId))
			{
				return false;
			}
		}

		return true;
	}

	private static bool Contains(IReadOnlyList<BlockDropEntryMsg> drops, ulong itemId)
	{
		foreach (var drop in drops)
		{
			if (drop.ItemId == itemId)
			{
				return true;
			}
		}

		return false;
	}

	private static bool Contains(IReadOnlyList<TrapDropEntryMsg> drops, ulong itemId)
	{
		foreach (var drop in drops)
		{
			if (drop.ItemId == itemId)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>One cell's outstanding drop set: the break's reported position (the wire's <c>Position</c>) plus the two drop families the break message carries.</summary>
	private sealed class Entry(float posX, float posY)
	{
		internal float PosX { get; } = posX;

		internal float PosY { get; } = posY;

		internal List<BlockDropEntryMsg> Drops { get; } = [];

		internal List<TrapDropEntryMsg> BuildingDrops { get; } = [];

		internal bool IsEmpty => Drops.Count == 0 && BuildingDrops.Count == 0;

		internal bool ContainsItem(ulong itemId) =>
			Drops.Exists(drop => drop.ItemId == itemId) || BuildingDrops.Exists(drop => drop.ItemId == itemId);

		internal bool RemoveItem(ulong itemId) =>
			Drops.RemoveAll(drop => drop.ItemId == itemId) + BuildingDrops.RemoveAll(drop => drop.ItemId == itemId) > 0;
	}
}
