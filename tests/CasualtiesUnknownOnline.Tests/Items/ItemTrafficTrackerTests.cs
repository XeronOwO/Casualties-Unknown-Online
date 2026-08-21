using System;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

public class ItemTrafficTrackerTests
{
	[Fact]
	public void Record_AccumulatesPerKindAndTotal()
	{
		var tracker = new ItemTrafficTracker(1000);

		tracker.Record(ItemTrafficKind.Spawn, "shell");
		tracker.Record(ItemTrafficKind.Spawn, "shell");
		tracker.Record(ItemTrafficKind.Drop, "shell");

		var window = tracker.Snapshot();
		Assert.Equal(3, window.Total);
		Assert.Equal(2, window.CountFor(ItemTrafficKind.Spawn));
		Assert.Equal(1, window.CountFor(ItemTrafficKind.Drop));
		Assert.Equal(0, window.CountFor(ItemTrafficKind.Move));
	}

	[Fact]
	public void TryCollectWindow_RollsAndResetsWithoutDoubleCounting()
	{
		var tracker = new ItemTrafficTracker(1000);
		tracker.Record(ItemTrafficKind.Spawn, "shell");

		Assert.False(tracker.TryCollectWindow(999, out _));
		Assert.True(tracker.TryCollectWindow(1000, out var window));
		Assert.Equal(1, window.Total);
		Assert.Equal(0, tracker.Snapshot().Total);
	}

	[Fact]
	public void TopItems_AreSortedByCountDescendingThenKey()
	{
		var tracker = new ItemTrafficTracker(1000);
		tracker.Record(ItemTrafficKind.Drop, "a");
		tracker.Record(ItemTrafficKind.Drop, "c");
		tracker.Record(ItemTrafficKind.Drop, "b");
		tracker.Record(ItemTrafficKind.Drop, "a");

		var top = tracker.Snapshot().TopItems;
		Assert.Equal("a", top[0].ItemId);
		Assert.Equal(2, top[0].Count);
		Assert.Equal("b", top[1].ItemId);
		Assert.Equal("c", top[2].ItemId);
	}

	[Fact]
	public void Snapshot_DoesNotResetTheWindow()
	{
		var tracker = new ItemTrafficTracker(1000);
		tracker.Record(ItemTrafficKind.Spawn, "shell");

		Assert.Equal(1, tracker.Snapshot().Total);
		Assert.Equal(1, tracker.Snapshot().Total);
	}

	[Fact]
	public void Constructor_RejectsNonPositiveWindow() =>
		Assert.Throws<ArgumentOutOfRangeException>(() => new ItemTrafficTracker(0));
}
