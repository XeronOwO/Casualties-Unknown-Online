using System;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The two per-frame correction lines the guest's world-item follow pump writes (<c>settle</c> and
/// <c>snap</c>), each behind its own repetition window, with the lines a window refused counted so ONE
/// summary can carry a whole run of them. Both lines fire per item per frame while an item's gap to the
/// host's state does not close — which is exactly what a diverged world looks like — so the pairing of
/// window and counter lives here once instead of beside each call site.
///
/// <para>
/// The window's subject is the (item, distance BUCKET) pair: a subject that stands still costs the window,
/// and one that MOVES reports again because the guard's anchor test sees the bucket change
/// (<see cref="ItemMotionState.DivergenceKeyDistance"/>). The count is kept per subject, not per item, so a
/// gap that slides through several bands reports its first line in each and each band carries its own
/// total. <see cref="Finished"/> ends a subject when the gap closes: it writes back the count AND re-arms
/// the window, so a divergence that returns at the same distance later reports its first line again
/// instead of being refused as a repeat. Pinned by <c>LogRepetitionGuardTests</c>.
/// </para>
/// </summary>
internal sealed class ItemDistanceLog(int suppressAfter, int capacity = 64)
{
	/// <summary>Which of the two lines — each keeps its own window and its own counts.</summary>
	internal enum Kind
	{
		Settle,
		Snap,
	}

	private readonly LogRepetitionGuard _settle = new(suppressAfter, capacity);
	private readonly LogRepetitionGuard _snap = new(suppressAfter, capacity);
	private readonly DistanceLogWindow _settleRefused = new(capacity);
	private readonly DistanceLogWindow _snapRefused = new(capacity);

	/// <summary>How many subjects the two counters are holding back right now (the context a first line carries).</summary>
	internal int Outstanding => _settleRefused.Count + _snapRefused.Count;

	/// <summary>A line is about to be written for this item at this distance — what does its window say?</summary>
	internal bool ShouldLog(Kind kind, ulong itemId, float distance, out int repeat) =>
		Guard(kind).TryLog(Key(itemId, distance), Bucket(distance), out repeat);

	/// <summary>Count a line the window refused, so the subject's summary can say how many.</summary>
	internal void Repeated(Kind kind, ulong itemId, float distance) =>
		_ = Table(kind).Add(Key(itemId, distance), distance);

	/// <summary>
	/// The item's gap closed at <paramref name="last"/> (or it left the world): hand back what its window
	/// swallowed — once — and RE-ARM the window, so the next divergence at the same distance is news again
	/// rather than a repeat of this one.
	/// </summary>
	internal bool Finished(Kind kind, ulong itemId, float last, out int suppressed)
	{
		var key = Key(itemId, last);
		var finished = Table(kind).TryFinished(key, out suppressed, out _);
		if (finished)
		{
			_ = Guard(kind).TryFlush(key, out _); // the window's history for this subject ends with the divergence
		}

		return finished;
	}

	/// <summary>Every subject still held back, in first-seen order (the pump's wind-down flush).</summary>
	internal void Each(Kind kind, Action<ItemDivergenceKey, int, float> report) => Table(kind).Each(report);

	/// <summary>Forget everything (session end, world teardown, unbind).</summary>
	internal void Clear()
	{
		_settle.Clear();
		_snap.Clear();
		_settleRefused.Clear();
		_snapRefused.Clear();
	}

	private static ItemDivergenceKey Key(ulong itemId, float distance) =>
		new(itemId, Bucket(distance));

	private static int Bucket(float distance) => ItemMotionState.DivergenceKeyDistance(distance);

	private LogRepetitionGuard Guard(Kind kind) =>
		kind == Kind.Settle ? _settle : _snap;

	private DistanceLogWindow Table(Kind kind) =>
		kind == Kind.Settle ? _settleRefused : _snapRefused;
}
