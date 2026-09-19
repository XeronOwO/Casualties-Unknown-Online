using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The guest-side pending BREAK-DROP table: block cell → the drops a local break
/// produced, appended on a second break at the same cell, bounded by the cap, and
/// cleared by the host's relay of those drops. The wire-level recovery loop is
/// GuestBreakDropRecoveryTests; this covers the table's own contract (including
/// the cap, which the integration path cannot reach without 65 536 cells, and the
/// acknowledgement rule, which is per ITEM ID — the ticket's duplicate guard).
/// </summary>
public class PendingBreakDropTableTests
{
	private static BlockDropEntryMsg Drop(ulong itemId) => new() { ItemId = itemId };

	private static TrapDropEntryMsg BuildingDrop(ulong itemId) => new() { ItemId = itemId };

	[Fact]
	public void Report_AppendsEveryDropOfTheBreaksAtThatCell()
	{
		var table = new PendingBreakDropTable();

		Assert.True(table.Report(3, 4, [Drop(77)], null));
		Assert.True(table.Report(3, 4, [Drop(78)], [BuildingDrop(90)]));
		Assert.True(table.Report(-2, 9, [Drop(79)], null));

		Assert.Equal(2, table.Count);
		var entry = Assert.Single(table.Entries, e => e.X == 3 && e.Y == 4);
		Assert.Equal(2, entry.Drops.Count);
		Assert.Contains(entry.Drops, d => d.ItemId == 77);
		Assert.Contains(entry.Drops, d => d.ItemId == 78);
		Assert.Equal(90ul, Assert.Single(entry.BuildingDrops).ItemId);
	}

	[Fact]
	public void Report_AtCap_RefusesNewCellsButStillUpdatesExisting()
	{
		var table = new PendingBreakDropTable(cap: 2);
		Assert.True(table.Report(1, 1, [Drop(1)], null));
		Assert.True(table.Report(2, 2, [Drop(2)], null));

		Assert.False(table.Report(3, 3, [Drop(3)], null));
		Assert.Equal(2, table.Count);

		Assert.True(table.Report(1, 1, [Drop(4)], null), "an existing cell always updates — its drops are real items");
		Assert.Equal(2, Assert.Single(table.Entries, e => e.X == 1 && e.Y == 1).Drops.Count);
	}

	[Fact]
	public void Answer_RemovesOnlyWhenTheRelayCarriesEveryOutstandingItem()
	{
		var table = new PendingBreakDropTable();
		table.Report(5, 7, [Drop(77), Drop(78)], [BuildingDrop(90)]);

		Assert.False(table.Answer(5, 7, [Drop(77)], null), "a partial relay leaves the rest outstanding");
		Assert.False(table.Answer(5, 7, [Drop(77), Drop(78)], null), "the building drop is still unanswered");
		Assert.False(table.Answer(6, 7, [Drop(77), Drop(78)], [BuildingDrop(90)]), "another cell's relay is not this break's answer");
		Assert.Equal(1, table.Count);

		// The positive control is the EXACT set (no extra id), so the rule is
		// pinned in both directions rather than only via over-naming.
		Assert.True(table.Answer(5, 7, [Drop(77), Drop(78)], [BuildingDrop(90)]));
		Assert.Equal(0, table.Count);
		Assert.False(table.Answer(5, 7, [Drop(77), Drop(78)], [BuildingDrop(90)]), "a second answer is a no-op");
	}

	[Fact]
	public void Answer_EmptyBlockFamily_IsCoveredByAnEmptyRelayList()
	{
		// A break whose drops are all building-death drops rides the same message
		// with an empty block-drop list — the acknowledgement must not depend on
		// that list being non-empty.
		var table = new PendingBreakDropTable();
		table.Report(5, 7, null, [BuildingDrop(90)]);

		Assert.True(table.Answer(5, 7, null, [BuildingDrop(90)]));
		Assert.Equal(0, table.Count);
	}

	[Fact]
	public void ForgetItem_DropsJustThatDrop_AndTheEntryWhenItEmpties()
	{
		var table = new PendingBreakDropTable();
		table.Report(5, 7, [Drop(77), Drop(78)], null);
		table.Report(9, 9, [Drop(90)], null);

		Assert.True(table.ForgetItem(77), "the refused drop leaves the outstanding set");
		Assert.Equal(2, table.Count);
		Assert.Single(Assert.Single(table.Entries, e => e.X == 5 && e.Y == 7).Drops);

		Assert.True(table.ForgetItem(78));
		Assert.Equal(1, table.Count);
		Assert.Equal(9, Assert.Single(table.Entries).X);

		Assert.False(table.ForgetItem(12345), "an item nobody is waiting for is a no-op");
	}

	[Fact]
	public void Clear_EmptiesEveryCell()
	{
		var table = new PendingBreakDropTable();
		table.Report(1, 1, [Drop(1)], null);
		table.Report(2, 2, [Drop(2)], [BuildingDrop(3)]);

		table.Clear();

		Assert.Equal(0, table.Count);
		Assert.Empty(table.Entries);
	}

	[Fact]
	public void Entries_CarryTheBreaksCellAndBothFamilies()
	{
		var table = new PendingBreakDropTable();
		table.Report(5, 7, [Drop(77)], [BuildingDrop(90)]);

		var entry = Assert.Single(table.Entries);

		Assert.Equal((5, 7), (entry.X, entry.Y));
		Assert.Equal(77ul, Assert.Single(entry.Drops).ItemId);
		Assert.Equal(90ul, Assert.Single(entry.BuildingDrops).ItemId);
	}

	[Fact]
	public void DefaultCap_MatchesTheSiblingPendingTables()
	{
		Assert.Equal(PendingBlockReportTable.DefaultCap, PendingBreakDropTable.DefaultCap);
		Assert.Equal(PendingBreakDropTable.DefaultCap, new PendingBreakDropTable().Cap);
	}
}
