using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The guest-side pending PARTIAL-damage table: cell → absolute damage, upsert on
/// a later hit, bounded by the cap, dropped by the host's answer or an air write.
/// The wire-level recovery loop is GuestBlockDamageReportRecoveryTests; this
/// covers the table's own contract (including the cap, which the integration path
/// cannot reach without 65 536 cells).
/// </summary>
public class PendingBlockDamageTableTests
{
	[Fact]
	public void Report_UpsertsTheCellsAbsoluteDamage()
	{
		var table = new PendingBlockDamageTable();

		Assert.True(table.Report(3, 4, 20f));
		Assert.True(table.Report(3, 4, 55f));
		Assert.True(table.Report(-2, 9, 5f));

		Assert.Equal(2, table.Count);
		Assert.Contains(table.Entries, e => e.X == 3 && e.Y == 4 && e.Damage == 55f);
		Assert.Contains(table.Entries, e => e.X == -2 && e.Y == 9 && e.Damage == 5f);
	}

	[Fact]
	public void Report_AtCap_RefusesNewCellsButStillUpdatesExisting()
	{
		var table = new PendingBlockDamageTable(cap: 2);
		Assert.True(table.Report(1, 1, 10f));
		Assert.True(table.Report(2, 2, 10f));

		Assert.False(table.Report(3, 3, 10f));
		Assert.Equal(2, table.Count);

		Assert.True(table.Report(1, 1, 30f), "an existing cell always updates — the host only needs its current value");
		Assert.Contains(table.Entries, e => e.X == 1 && e.Y == 1 && e.Damage == 30f);
	}

	[Fact]
	public void Remove_DropsOnlyTheRequestedCell()
	{
		var table = new PendingBlockDamageTable();
		table.Report(1, 1, 10f);
		table.Report(2, 2, 10f);

		Assert.True(table.Remove(1, 1));
		Assert.False(table.Remove(1, 1), "a second remove is a no-op");
		Assert.Equal(1, table.Count);
		var remaining = Assert.Single(table.Entries);
		Assert.True(remaining.X == 2 && remaining.Y == 2, $"the untouched cell must survive, got ({remaining.X},{remaining.Y})");
	}

	[Fact]
	public void Clear_EmptiesEveryCell()
	{
		var table = new PendingBlockDamageTable();
		table.Report(1, 1, 10f);
		table.Report(2, 2, 10f);

		table.Clear();

		Assert.Equal(0, table.Count);
		Assert.Empty(table.Entries);
	}

	[Fact]
	public void Entries_CarryTheWireShape()
	{
		var table = new PendingBlockDamageTable();
		table.Report(7, 8, 12.5f);

		var entry = Assert.Single(table.Entries);
		Assert.Equal(7, entry.X);
		Assert.Equal(8, entry.Y);
		Assert.Equal(12.5f, entry.Damage);
	}

	[Fact]
	public void DefaultCap_MatchesTheBlockStatePendingBound()
	{
		Assert.Equal(PendingBlockReportTable.DefaultCap, PendingBlockDamageTable.DefaultCap);
		Assert.Equal(PendingBlockDamageTable.DefaultCap, new PendingBlockDamageTable().Cap);
		Assert.Empty(new PendingBlockDamageTable().Entries);
	}
}
