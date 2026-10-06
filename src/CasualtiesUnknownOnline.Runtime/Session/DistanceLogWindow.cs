using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The subjects whose repeated lines a <see cref="LogRepetitionGuard"/> window is still swallowing, with
/// the last value seen and how many lines went unwritten. A caller adds an entry when its line is refused
/// and finishes it when the repetition ends, so the log carries the window's first lines, one summary per
/// finished run, and a wind-down flush for the entries still repeating when their table empties — instead
/// of a line per frame per subject. Bounded: past <c>capacity</c> the table stops growing (the oldest
/// entry is dropped), so a repeating world cannot trade a log storm for a memory one. The first lines
/// already locate the defect; <see cref="Add"/> returns false for every entry past the cap and those lines
/// are then counted NOWHERE, so the summaries stay indicative rather than complete past that point.
/// Pinned by <c>LogRepetitionGuardTests</c>.
/// </summary>
internal sealed class DistanceLogWindow(int capacity = 64)
{
	private readonly Dictionary<ItemDivergenceKey, Entry> _entries = [];
	private readonly List<ItemDivergenceKey> _order = [];

	internal int Capacity { get; } = capacity >= 1
		? capacity
		: throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "a table of zero could never report a finished run");

	internal int Count => _entries.Count;

	/// <summary>Record what this frame swallowed for the subject — false when the table is full (the line is then counted nowhere).</summary>
	internal bool Add(ItemDivergenceKey key, float last)
	{
		if (_entries.TryGetValue(key, out var entry))
		{
			entry.Suppressed++;
			entry.Last = last;
			return true;
		}

		if (_entries.Count >= Capacity)
		{
			return false;
		}

		_entries[key] = new Entry { Suppressed = 1, Last = last };
		_order.Add(key);
		return true;
	}

	/// <summary>The subject's repetition ended — hand back what the window swallowed and the last value, once.</summary>
	internal bool TryFinished(ItemDivergenceKey key, out int suppressed, out float last)
	{
		if (_entries.TryGetValue(key, out var entry))
		{
			suppressed = entry.Suppressed;
			last = entry.Last;
			_entries.Remove(key);
			_order.Remove(key);
			return true;
		}

		suppressed = 0;
		last = 0f;
		return false;
	}

	/// <summary>Every outstanding entry, in first-seen order (the wind-down flush).</summary>
	internal void Each(Action<ItemDivergenceKey, int, float> report)
	{
		foreach (var key in _order)
		{
			var entry = _entries[key];
			report(key, entry.Suppressed, entry.Last);
		}
	}

	internal void Clear()
	{
		_entries.Clear();
		_order.Clear();
	}

	private sealed class Entry
	{
		internal int Suppressed;
		internal float Last;
	}
}
