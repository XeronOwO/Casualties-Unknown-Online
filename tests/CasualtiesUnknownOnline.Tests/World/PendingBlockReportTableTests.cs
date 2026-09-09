using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The guest-side pending block-report table: cell → block, upsert on a newer
/// local write, bounded by the cap, cleared by the host's answer. The wire-level
/// recovery loop is GuestBlockReportRecoveryTests; this covers the table's own
/// contract (including the cap, which the integration path cannot reach without
/// 65 536 cells).
/// </summary>
public class PendingBlockReportTableTests
{
	[Fact]
	public void Report_UpsertsTheCellsCurrentBlock()
	{
		var table = new PendingBlockReportTable();

		Assert.True(table.Report(3, 4, 0));
		Assert.True(table.Report(3, 4, 7));
		Assert.True(table.Report(-2, 9, 0));

		Assert.Equal(2, table.Count);
		Assert.Contains(new DamagedBlock(3, 4, 7), table.Entries);
		Assert.Contains(new DamagedBlock(-2, 9, 0), table.Entries);
	}

	[Fact]
	public void Report_AtCap_RefusesNewCellsButStillUpdatesExisting()
	{
		var table = new PendingBlockReportTable(cap: 2);
		Assert.True(table.Report(1, 1, 0));
		Assert.True(table.Report(2, 2, 0));

		Assert.False(table.Report(3, 3, 0));
		Assert.Equal(2, table.Count);

		Assert.True(table.Report(1, 1, 5), "an existing cell always updates — the host only needs its current value");
		Assert.Contains(new DamagedBlock(1, 1, 5), table.Entries);
	}

	[Fact]
	public void Remove_DropsOnlyTheRequestedCell()
	{
		var table = new PendingBlockReportTable();
		table.Report(1, 1, 0);
		table.Report(2, 2, 0);

		Assert.True(table.Remove(1, 1));
		Assert.False(table.Remove(1, 1), "a second remove is a no-op");
		Assert.Equal(1, table.Count);
		Assert.Equal(new DamagedBlock(2, 2, 0), Assert.Single(table.Entries));
	}

	[Fact]
	public void Clear_EmptiesEveryCell()
	{
		var table = new PendingBlockReportTable();
		table.Report(1, 1, 0);
		table.Report(2, 2, 0);

		table.Clear();

		Assert.Equal(0, table.Count);
		Assert.Empty(table.Entries);
	}

	[Fact]
	public void DefaultCap_MatchesTheHostDeviationTableBound()
	{
		Assert.Equal(WorldStateMessageService.MaxDamagedBlocks, PendingBlockReportTable.DefaultCap);
		Assert.Equal(PendingBlockReportTable.DefaultCap, new PendingBlockReportTable().Cap);
	}
}
