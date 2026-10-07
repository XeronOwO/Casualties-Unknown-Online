using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The repetition bound the high-frequency diagnostics ask before they log
/// (<see cref="LogRepetitionGuard"/>, <see cref="DistanceLogWindow"/> and the
/// <see cref="ItemDistanceLog"/> shell that pairs them): a value that repeats on every frame or every
/// snapshot must cost a BOUNDED number of lines, a value that MOVES is news again, and the end of a run is
/// reported once with what it suppressed — including a run that ends and comes back, which must report its
/// first line rather than be refused as a repeat. The defect this pins is batch `20261005-b`'s storm: a
/// diverged guest wrote one `[ItemPhysics] settle` line per item per frame for minutes (33.4 MB in four
/// minutes on a 0.8 MB log).
/// </summary>
public class LogRepetitionGuardTests
{
	[Fact]
	public void TheFirstReport_IsWritten_AndSaysSo()
	{
		var guard = new LogRepetitionGuard(suppressAfter: 8);

		Assert.True(guard.TryLog("item", "6.31", out var repeat));

		Assert.Equal(0, repeat);
	}

	[Fact]
	public void Repeats_CountUpToTheWindow()
	{
		var guard = new LogRepetitionGuard(suppressAfter: 8);
		guard.TryLog("item", "6.31", out _);

		for (var i = 1; i <= 7; i++)
		{
			Assert.True(guard.TryLog("item", "6.31", out var repeat), $"repeat {i} is still inside the window");
			Assert.Equal(i, repeat);
		}
	}

	[Fact]
	public void BeyondTheWindow_TheSameValueStopsWriting()
	{
		var guard = new LogRepetitionGuard(suppressAfter: 8);
		for (var i = 1; i <= 8; i++)
		{
			Assert.True(guard.TryLog("item", "6.31", out _), $"report {i} is one of the window's own lines");
		}

		for (var i = 0; i < 20; i++)
		{
			Assert.False(guard.TryLog("item", "6.31", out _), "the ninth and every later identical line is refused");
		}

		Assert.True(20 == guard.Suppressed("item"), $"the guard counted every refused line (got {guard.Suppressed("item")})");
	}

	[Fact]
	public void AChangedKey_IsNewsWithAFreshWindow()
	{
		var guard = new LogRepetitionGuard(suppressAfter: 2);
		guard.TryLog("band-126", "6.31", out _);
		guard.TryLog("band-126", "6.31", out _);
		Assert.False(guard.TryLog("band-126", "6.31", out _), "the window is spent");

		Assert.True(guard.TryLog("band-180", "9.02", out var repeat), "a moved subject is a different key, and it is news");

		Assert.Equal(0, repeat);
		Assert.True(0 == guard.Suppressed("band-180"), $"the fresh window starts clean (got {guard.Suppressed("band-180")})");
	}

	[Fact]
	public void AWindow_EndsAndRearmsOnFlush()
	{
		var guard = new LogRepetitionGuard(suppressAfter: 1);
		guard.TryLog("item", "6.31", out _);
		Assert.False(guard.TryLog("item", "6.31", out _), "the window is spent");
		Assert.False(guard.TryLog("item", "6.31", out _));

		Assert.True(guard.TryFlush("item", out var suppressed), "the flush hands back what the window swallowed");

		Assert.Equal(2, suppressed);
		Assert.True(guard.TryLog("item", "6.31", out var repeat), "after the flush the same value is news again");
		Assert.Equal(0, repeat);
		Assert.True(1 == guard.TrackedKeys, $"the flushed window reopened for the same value (got {guard.TrackedKeys})");
	}

	[Fact]
	public void AnotherKey_ReportsItsOwnFirstLine()
	{
		var guard = new LogRepetitionGuard(suppressAfter: 1);
		guard.TryLog("item", "6.31", out _);
		Assert.False(guard.TryLog("item", "6.31", out _), "the first key's window is spent");

		Assert.True(guard.TryLog("item@1.02", "1.02", out var repeat), "another subject reports its own first line");

		Assert.Equal(0, repeat);
		Assert.True(0 == guard.Suppressed("item@1.02"), "the fresh window starts clean");
	}

	[Fact]
	public void Keys_AreIndependent()
	{
		var guard = new LogRepetitionGuard(suppressAfter: 1);
		guard.TryLog("item-a", "6.31", out _);
		Assert.False(guard.TryLog("item-a", "6.31", out _), "the first key's window is spent");

		Assert.True(guard.TryLog("item-b", "6.31", out _), "another key has its own window");

		Assert.True(2 == guard.TrackedKeys, $"two keys are tracked (got {guard.TrackedKeys})");
	}

	[Fact]
	public void TheGuard_StopsGrowingPastItsCapacity()
	{
		var guard = new LogRepetitionGuard(suppressAfter: 1, capacity: 2);

		Assert.True(guard.TryLog("a", "1", out _));
		Assert.True(guard.TryLog("b", "1", out _));
		Assert.True(guard.TryLog("c", "1", out _), "a new subject still reports");

		Assert.True(2 == guard.TrackedKeys, $"the oldest entry was dropped (got {guard.TrackedKeys})");
	}

	[Fact]
	public void TheWindow_NamesTheSubjectsItIsStillHolding()
	{
		var guard = new LogRepetitionGuard(suppressAfter: 1);
		guard.TryLog("member-1", "1", out _);
		Assert.False(guard.TryLog("member-1", "1", out _), "the first subject's window is spent");
		guard.TryLog("member-2", "1", out _);

		Assert.Equal(["member-1", "member-2"], guard.Subjects);
	}

	[Fact]
	public void TheSubjectList_IsACopyTheWindDownCanFlushThrough()
	{
		var guard = new LogRepetitionGuard(suppressAfter: 1);
		guard.TryLog("member-1", "1", out _);
		Assert.False(guard.TryLog("member-1", "1", out _));
		Assert.False(guard.TryLog("member-1", "1", out _));
		guard.TryLog("member-2", "1", out _);

		// The wind-down reads the list while TryFlush drops the entries it reports — the copy is what makes
		// that legal, and only the subject that cost lines comes back.
		var reported = new List<(string Key, int Suppressed)>();
		foreach (var subject in guard.Subjects)
		{
			if (guard.TryFlush(subject, out var suppressed))
			{
				reported.Add(((string)subject, suppressed));
			}
		}

		Assert.Equal(1, reported.Count);
		Assert.Equal("member-1", reported[0].Key);
		Assert.Equal(2, reported[0].Suppressed);
		Assert.True(0 == guard.TrackedKeys, $"the wind-down forgot every subject (got {guard.TrackedKeys})");
	}

	[Fact]
	public void TheSubjectList_IsBoundedByTheCapacityAndEmptiedByClear()
	{
		var guard = new LogRepetitionGuard(suppressAfter: 1, capacity: 2);
		guard.TryLog("a", "1", out _);
		guard.TryLog("b", "1", out _);
		guard.TryLog("c", "1", out _);

		var subjects = guard.Subjects;
		Assert.Equal(["b", "c"], subjects);

		guard.Clear();
		Assert.True(0 == guard.Subjects.Count, $"a cleared window holds nothing (got {guard.Subjects.Count})");
		Assert.Equal(["b", "c"], subjects);
	}

	[Fact]
	public void AStormOfOneThousandIdenticalFrames_CostsTheWindowOnly()
	{
		var guard = new LogRepetitionGuard(suppressAfter: 8);
		var lines = 0;
		for (var frame = 0; frame < 1000; frame++)
		{
			if (guard.TryLog("item", "6.31", out _))
			{
				lines++;
			}
		}

		Assert.Equal(8, lines);
	}

	[Fact]
	public void WindowOfOne_WritesTheFirstLineOnly()
	{
		var guard = new LogRepetitionGuard(suppressAfter: 1);

		Assert.True(guard.TryLog("pair", "AA|BB", out _));
		Assert.False(guard.TryLog("pair", "AA|BB", out _));
	}

	[Fact]
	public void ZeroWindow_IsRefused() =>
		Assert.Throws<ArgumentOutOfRangeException>(ZeroWindow);

	private static void ZeroWindow() => _ = new LogRepetitionGuard(suppressAfter: 0);

	[Fact]
	public void TheDistanceLog_ClosesAndRearmsOnResolution()
	{
		var log = new ItemDistanceLog(suppressAfter: 1);
		Assert.True(log.ShouldLog(ItemDistanceLog.Kind.Settle, 7UL, 6.31f, out _));
		Assert.False(log.ShouldLog(ItemDistanceLog.Kind.Settle, 7UL, 6.31f, out _), "the standing gap is refused");
		log.Repeated(ItemDistanceLog.Kind.Settle, 7UL, 6.31f);

		Assert.True(log.Finished(ItemDistanceLog.Kind.Settle, 7UL, 6.31f, out var suppressed), "the gap closed");

		Assert.Equal(1, suppressed);
		Assert.False(log.Finished(ItemDistanceLog.Kind.Settle, 7UL, 6.31f, out _), "and only once");
		Assert.True(
			log.ShouldLog(ItemDistanceLog.Kind.Settle, 7UL, 6.29f, out var repeat),
			"a divergence that comes back at the SAME distance band reports its first line, not a repeat of the one that ended");
		Assert.Equal(0, repeat);
	}

	[Fact]
	public void TheDistanceLog_KeepsTheTwoLinesApart()
	{
		var log = new ItemDistanceLog(suppressAfter: 1);

		Assert.True(log.ShouldLog(ItemDistanceLog.Kind.Settle, 7UL, 6.31f, out _), "the first settle line reports");
		Assert.False(log.ShouldLog(ItemDistanceLog.Kind.Settle, 7UL, 6.31f, out _), "the identical settle line is refused");
		Assert.True(log.ShouldLog(ItemDistanceLog.Kind.Snap, 7UL, 6.31f, out _), "the snap line has its own window");

		log.Repeated(ItemDistanceLog.Kind.Settle, 7UL, 5.0f);
		Assert.True(1 == log.Outstanding, $"the refused settle line is counted (got {log.Outstanding})");
	}

	[Fact]
	public void TheDistanceLog_CountsPerSubjectAndMovesWithTheBand()
	{
		var log = new ItemDistanceLog(suppressAfter: 1);
		log.ShouldLog(ItemDistanceLog.Kind.Settle, 7UL, 6.31f, out _);
		log.Repeated(ItemDistanceLog.Kind.Settle, 7UL, 6.31f);

		Assert.True(log.ShouldLog(ItemDistanceLog.Kind.Settle, 7UL, 9.02f, out _), "a gap that moved is news again");

		Assert.True(1 == log.Outstanding, $"the old band keeps its own count (got {log.Outstanding})");
	}

	[Fact]
	public void TheDistanceLog_FlushesWhatItHeldBack_InFirstSeenOrder()
	{
		var log = new ItemDistanceLog(suppressAfter: 1);
		log.Repeated(ItemDistanceLog.Kind.Settle, 7UL, 6.31f);
		log.Repeated(ItemDistanceLog.Kind.Settle, 9UL, 1.25f);
		log.Repeated(ItemDistanceLog.Kind.Settle, 7UL, 6.31f);

		var flushed = new List<(ulong, int, float)>();
		log.Each(ItemDistanceLog.Kind.Settle, (key, suppressed, last) => flushed.Add((key.ItemId, suppressed, last)));
		log.Clear();

		Assert.Equal(2, flushed.Count);
		Assert.Equal((7UL, 2, 6.31f), flushed[0]);
		Assert.Equal((9UL, 1, 1.25f), flushed[1]);
		Assert.True(0 == log.Outstanding, "the table is empty after the flush");
	}

	[Fact]
	public void TheDistanceWindow_StopsCountingPastItsCap()
	{
		var window = new DistanceLogWindow(capacity: 2);

		Assert.True(window.Add(Key(7UL, 6.31f), 6.31f), "the first entry fits");
		Assert.True(window.Add(Key(9UL, 1.25f), 1.25f), "the second entry fits");
		Assert.False(window.Add(Key(11UL, 3f), 3f), "the table is full, and the caller is told so");

		Assert.True(2 == window.Count, $"the entry table is capped (got {window.Count})");
		Assert.False(window.TryFinished(Key(11UL, 3f), out _, out _), "an entry past the cap can never finish");
	}

	private static ItemDivergenceKey Key(ulong itemId, float distance) =>
		new(itemId, ItemMotionState.DivergenceKeyDistance(distance));
}
