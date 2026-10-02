using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The receiver's deferred world-item rows: rows whose local generation object had not landed
/// yet when the authoritative table was applied (the re-entry window — batch 20261002-i measured
/// 9 (guest) / 14 (alternate) rows that never bound, each leaving an id-less local object beside
/// a materialized copy). The owner retries the rows against the scene every frame and adopts
/// (binds) one as soon as its generation-time object appears; a row that outlives its grace is
/// handed back for ordinary materialization, so an authoritative item can never be lost to a
/// slow local load.
///
/// Keyed by instance id: a repeated snapshot (the periodic keyframe re-delivers the same table)
/// refreshes a row's payload but keeps its ORIGINAL deadline — a per-arrival reset would defer the
/// materialization forever. Pure bookkeeping: no Unity types and no scene access; the
/// adopt-or-materialize decision stays with the caller.
/// </summary>
public sealed class PendingWorldItemRows
{
	private readonly Dictionary<ulong, Row> _rows = [];
	private readonly List<ulong> _order = [];

	private readonly record struct Row(WorldItem Entry, double AddedAtSeconds);

	/// <summary>Add a row, or refresh the payload of one already waiting. Returns true when the row is new.</summary>
	public bool AddOrRefresh(WorldItem entry, double nowSeconds)
	{
		if (_rows.TryGetValue(entry.ItemId, out var existing))
		{
			_rows[entry.ItemId] = existing with { Entry = entry };
			return false;
		}

		_rows[entry.ItemId] = new Row(entry, nowSeconds);
		_order.Add(entry.ItemId);
		return true;
	}

	/// <summary>True when this instance id is still waiting (a deferred row, not a refusal).</summary>
	public bool Contains(ulong itemId) => _rows.ContainsKey(itemId);

	/// <summary>Stop tracking a row — it landed through another path.</summary>
	public bool Remove(ulong itemId)
	{
		if (!_rows.Remove(itemId))
		{
			return false;
		}

		_order.Remove(itemId);
		return true;
	}

	public void Clear()
	{
		_rows.Clear();
		_order.Clear();
	}

	public int Count => _rows.Count;

	/// <summary>The rows still waiting, oldest first — the caller retries these against the scene. The list is overwritten, not appended to.</summary>
	public void CopyWaiting(List<WorldItem> into)
	{
		into.Clear();
		foreach (var id in _order)
		{
			if (_rows.TryGetValue(id, out var row))
			{
				into.Add(row.Entry);
			}
		}
	}

	/// <summary>Move every row whose grace expired into <paramref name="expired"/> (oldest first) and stop tracking it — the caller materializes it the ordinary way.</summary>
	public void TakeExpired(double nowSeconds, double graceSeconds, List<WorldItem> expired)
	{
		expired.Clear();
		for (var i = _order.Count - 1; i >= 0; i--)
		{
			var id = _order[i];
			if (!_rows.TryGetValue(id, out var row) || nowSeconds - row.AddedAtSeconds < graceSeconds)
			{
				continue;
			}

			expired.Add(row.Entry);
			_rows.Remove(id);
			_order.RemoveAt(i);
		}

		// The reverse walk yields newest-first; the caller reads oldest-first.
		expired.Reverse();
	}
}
