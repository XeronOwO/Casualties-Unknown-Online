using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The repetition bound a diagnostic asks before it writes a line. A value that repeats is news ONCE
/// and noise afterwards: the same divergence arriving every frame, or every snapshot, used to cost one
/// line per occurrence — which is how batch `20261005-b`'s guest grew a 0.8 MB log to 33.4 MB in about
/// four minutes.
///
/// <para>
/// The subject of a window is a key the caller chooses, and the key must CARRY the value: the (item,
/// distance BUCKET) pair for the item lines, the pair text for a baseline divergence, the rectangle for a
/// fluid region. One subject costs <see cref="SuppressAfter"/> lines for a standing divergence —
/// <see cref="TryLog"/> returns true for those and false afterwards, counting what it swallowed so the
/// caller can write the one summary that turns a bound into a complete story — and a subject that MOVES
/// reports again, because a moved value is a different key. <see cref="TryFlush"/> ends a subject and drops
/// its entry, which is what makes a divergence that resolves and later RETURNS report its first line again
/// instead of being refused as a repeat of the one that ended: <c>ItemDistanceLog.Finished</c> is the
/// caller that does it per item. Entries are bounded by <c>capacity</c> (the oldest goes first), so a
/// diagnostic whose subject keeps moving — the item lines do, and measured over a real 12.5-minute session
/// that was 12 keys — cannot trade a log storm for a memory one. Pinned by <c>LogRepetitionGuardTests</c>.
/// </para>
/// </summary>
internal sealed class LogRepetitionGuard(int suppressAfter, int capacity = 256)
{
	private readonly Dictionary<object, Entry> _entries = [];
	private readonly List<object> _order = [];

	/// <summary>How many reports of one subject are written before the identical ones are refused.</summary>
	internal int SuppressAfter { get; } = suppressAfter >= 1
		? suppressAfter
		: throw new ArgumentOutOfRangeException(nameof(suppressAfter), suppressAfter, "a window of zero would swallow the first line as well");

	/// <summary>How many subjects a window keeps at once (the oldest is dropped past it).</summary>
	internal int Capacity { get; } = capacity >= 1
		? capacity
		: throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "a window of zero could never report anything");

	/// <summary>Subjects currently tracked — a leak probe for the guard's own tests.</summary>
	internal int TrackedKeys => _entries.Count;

	/// <summary>
	/// True = write the line now, with <paramref name="repeat"/> as the line's index in the window (0 for
	/// the first, so the log reads as "the third identical line"). False = the subject's window is spent
	/// and the caller must count the line with <see cref="Suppressed"/> instead of writing it.
	/// </summary>
	internal bool TryLog(object key, object? value, out int repeat)
	{
		if (!_entries.TryGetValue(key, out var entry))
		{
			Add(key, value);
			repeat = 0;
			return true;
		}

		entry.Value = value;
		if (entry.Step < SuppressAfter)
		{
			entry.Step++;
			repeat = entry.Step - 1;
			return true;
		}

		entry.Suppressed++;
		repeat = 0;
		return false;
	}

	/// <summary>Lines this subject was refused so far.</summary>
	internal int Suppressed(object key) => _entries.TryGetValue(key, out var entry) ? entry.Suppressed : 0;

	/// <summary>
	/// The subject stopped repeating: forget its window and hand back what it swallowed, so the caller can
	/// write the summary line that completes the story. False when the subject was untracked or cost
	/// nothing — the entry goes either way, because a window is a repetition bound and not a history.
	/// </summary>
	internal bool TryFlush(object key, out int suppressed)
	{
		if (_entries.TryGetValue(key, out var entry))
		{
			suppressed = entry.Suppressed;
			Remove(key);
			return suppressed > 0;
		}

		suppressed = 0;
		return false;
	}

	/// <summary>Forget every window (session end, world teardown, unbind).</summary>
	internal void Clear()
	{
		_entries.Clear();
		_order.Clear();
	}

	private void Add(object key, object? value)
	{
		if (_entries.Count >= Capacity)
		{
			var oldest = _order[0];
			_order.RemoveAt(0);
			_entries.Remove(oldest);
		}

		_entries[key] = new Entry { Value = value, Step = 1 };
		_order.Add(key);
	}

	private void Remove(object key)
	{
		_entries.Remove(key);
		_order.Remove(key);
	}

	private sealed class Entry
	{
		internal object? Value;
		internal int Step;
		internal int Suppressed;
	}
}
