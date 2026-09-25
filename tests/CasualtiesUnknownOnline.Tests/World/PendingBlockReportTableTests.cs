using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The guest-side pending block-report table: cell → the block this side wrote
/// there and that write's presentation claim, upsert on a newer local write,
/// bounded by the cap, cleared by the host's answer. The claim is part of the
/// entry because a fallback re-report can be the FIRST time the host learns of
/// that write: a lost break must not come back as a silent write. The wire-level
/// recovery loop is GuestBlockReportRecoveryTests; this covers the table's own
/// contract (including the cap, which the integration path cannot reach without
/// 65 536 cells).
/// </summary>
public class PendingBlockReportTableTests
{
	[Fact]
	public void Report_UpsertsTheCellsCurrentBlockAndItsClaim()
	{
		var table = new PendingBlockReportTable();

		Assert.True(table.Report(3, 4, 0, playerBreak: true));
		Assert.True(table.Report(3, 4, 7, playerBreak: false));
		Assert.True(table.Report(-2, 9, 0, playerBreak: false));

		Assert.Equal(2, table.Count);
		Assert.Contains(new PendingBlockReport(3, 4, 7), table.Entries);
		Assert.Contains(new PendingBlockReport(-2, 9, 0), table.Entries);
	}

	[Fact]
	public void Report_KeepsABreaksClaim_SoARecoveryReReportCannotSilenceIt()
	{
		var table = new PendingBlockReportTable();

		table.Report(5, 7, 0, playerBreak: true);

		Assert.True(Assert.Single(table.Entries).PlayerBreak, "the re-report is the host's first chance to learn of that write — it must carry the same claim the live report carried");
	}

	[Fact]
	public void Report_AtCap_RefusesNewCellsButStillUpdatesExisting()
	{
		var table = new PendingBlockReportTable(cap: 2);
		Assert.True(table.Report(1, 1, 0, playerBreak: true));
		Assert.True(table.Report(2, 2, 0, playerBreak: false));

		Assert.False(table.Report(3, 3, 0, playerBreak: true));
		Assert.Equal(2, table.Count);

		Assert.True(table.Report(1, 1, 5, playerBreak: false), "an existing cell always updates — the host only needs its current value and the newer write's own claim");
		Assert.Contains(new PendingBlockReport(1, 1, 5), table.Entries);
	}

	[Fact]
	public void Remove_DropsOnlyTheRequestedCell()
	{
		var table = new PendingBlockReportTable();
		table.Report(1, 1, 0, playerBreak: true);
		table.Report(2, 2, 0, playerBreak: false);

		Assert.True(table.Remove(1, 1));
		Assert.False(table.Remove(1, 1), "a second remove is a no-op");
		Assert.Equal(1, table.Count);
		Assert.Equal(new PendingBlockReport(2, 2, 0), Assert.Single(table.Entries));
	}

	[Fact]
	public void Clear_EmptiesEveryCell()
	{
		var table = new PendingBlockReportTable();
		table.Report(1, 1, 0, playerBreak: true);
		table.Report(2, 2, 0, playerBreak: false);

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
