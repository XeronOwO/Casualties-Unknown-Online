using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The receiver's deferred-row bookkeeping: a row whose local generation object had not landed
/// waits, and a repeated snapshot refreshes its payload but must NOT push its deadline out — the
/// periodic keyframe re-delivers the same table every few seconds, and a per-arrival reset would
/// defer the authority's copy forever. Pure data: no scene, no Unity.
/// </summary>
public class PendingWorldItemRowsTests
{
	private static WorldItem Entry(ulong id) => new(
		id, new CharacterItemMsg { ItemId = "test_item", Condition = 1f },
		new NetVector2(1f, 2f), new NetVector2(0f, 0f), 0, 0f, false);

	[Fact]
	public void AddOrRefresh_AFirstSightingIsNew_AndCounts()
	{
		var rows = new PendingWorldItemRows();

		Assert.True(rows.AddOrRefresh(Entry(1), 10.0));
		Assert.Equal(1, rows.Count);
	}

	[Fact]
	public void AddOrRefresh_ASecondSightingRefreshesThePayload_ButKeepsTheOriginalDeadline()
	{
		var rows = new PendingWorldItemRows();
		Assert.True(rows.AddOrRefresh(Entry(1), 10.0));

		// The keyframe re-delivers the same row 5 s later — it is not new, and it must not
		// reset the 2 s grace: the row expires at 12 s, not at 17 s.
		Assert.False(rows.AddOrRefresh(Entry(1), 15.0));
		Assert.Equal(1, rows.Count);

		var expired = new List<WorldItem>();
		rows.TakeExpired(12.0, 2.0, expired);

		Assert.Single(expired);
		Assert.Equal(1UL, expired[0].ItemId);
		Assert.Equal(0, rows.Count);
	}

	[Fact]
	public void TakeExpired_LeavesRowsInsideTheirGraceWaiting()
	{
		var rows = new PendingWorldItemRows();
		rows.AddOrRefresh(Entry(1), 10.0);
		rows.AddOrRefresh(Entry(2), 11.0);

		var expired = new List<WorldItem>();
		rows.TakeExpired(11.9, 2.0, expired);

		Assert.Empty(expired); // 1.9 s < 2 s
		Assert.Equal(2, rows.Count);
	}

	[Fact]
	public void TakeExpired_MovesEveryExpiredRowOldestFirst()
	{
		var rows = new PendingWorldItemRows();
		rows.AddOrRefresh(Entry(1), 10.0);
		rows.AddOrRefresh(Entry(2), 11.0);
		rows.AddOrRefresh(Entry(3), 15.0);

		var expired = new List<WorldItem>();
		rows.TakeExpired(13.0, 2.0, expired);

		Assert.Equal(2, expired.Count);
		Assert.Equal(1UL, expired[0].ItemId); // oldest first
		Assert.Equal(2UL, expired[1].ItemId);
		Assert.Equal(1, rows.Count); // the 15 s row is still inside its grace
	}

	[Fact]
	public void CopyWaiting_WalksOldestFirst_AndOverwritesTheTargetList()
	{
		var rows = new PendingWorldItemRows();
		rows.AddOrRefresh(Entry(1), 10.0);
		rows.AddOrRefresh(Entry(2), 11.0);
		var into = new List<WorldItem> { Entry(99) };

		rows.CopyWaiting(into);

		Assert.Equal(2, into.Count);
		Assert.Equal(1UL, into[0].ItemId);
		Assert.Equal(2UL, into[1].ItemId);
	}

	[Fact]
	public void Remove_StopsTrackingTheRow_AndReportsWhetherItWasThere()
	{
		var rows = new PendingWorldItemRows();
		rows.AddOrRefresh(Entry(1), 10.0);

		Assert.True(rows.Remove(1));
		Assert.False(rows.Remove(1));
		Assert.Equal(0, rows.Count);
		Assert.True(rows.AddOrRefresh(Entry(1), 20.0)); // re-added after removal is new again
	}

	[Fact]
	public void Clear_DropsEverything()
	{
		var rows = new PendingWorldItemRows();
		rows.AddOrRefresh(Entry(1), 10.0);
		rows.AddOrRefresh(Entry(2), 11.0);

		rows.Clear();

		Assert.Equal(0, rows.Count);
		var expired = new List<WorldItem>();
		rows.TakeExpired(100.0, 2.0, expired);
		Assert.Empty(expired);
	}
}
