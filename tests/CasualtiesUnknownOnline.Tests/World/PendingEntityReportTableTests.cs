using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The guest-side pending runtime-entity creation table: creation key → the
/// reported creation message + its fallback attempt count, upsert on a repeat,
/// bounded by the cap, cleared by the host's answer / the entity's death /
/// a world boundary. The wire-level recovery loop is
/// GuestEntityReportRecoveryTests; this covers the table's own contract
/// (including the cap, which the integration path cannot reach).
/// </summary>
public class PendingEntityReportTableTests
{
	private static EntitySpawnedMsg Creation(string id, float x, float y, string keypadCode = "", ulong creator = 0, uint sequence = 0) => new()
	{
		Id = id,
		Position = new NetVector2Msg(x, y),
		KeypadCode = keypadCode,
		CreatorSteamId = creator,
		CreationSequence = sequence,
	};

	private static RuntimeEntityKey Key(string id, int x, int y, ulong creator = 0, uint sequence = 0) =>
		new(id, x, y, creator, sequence);

	[Fact]
	public void Report_UpsertsTheCreationAndResetsTheAttemptCount()
	{
		var table = new PendingEntityReportTable();

		Assert.True(table.Report(Creation("keypad", 3f, 4f)));
		Assert.True(table.Report(Creation("keypad", 3f, 4f, keypadCode: "1234"))); // the same creation re-recorded
		Assert.True(table.Report(Creation("landmine", -2f, 9f)));

		Assert.Equal(2, table.Count);
		Assert.Contains(table.Entries, e => e.Msg.Id == "keypad" && e.Msg.KeypadCode == "1234" && e.Attempts == 0);
	}

	[Fact]
	public void Report_DistinctCellsOfTheSamePrefab_AreDistinctCreations()
	{
		var table = new PendingEntityReportTable();

		table.Report(Creation("turret", 0f, 0f));
		table.Report(Creation("turret", 1.5f, 0f));

		Assert.Equal(2, table.Count);
	}

	[Fact]
	public void Report_TwoCreationsOfTheSamePrefabInOneCell_AreDistinctByToken()
	{
		// Two identical prefabs 0.7 m apart share the floored cell — the
		// creating side's monotonic token is what keeps them apart, so the
		// second report never overwrites the first.
		var table = new PendingEntityReportTable();

		Assert.True(table.Report(Creation("turret", 5.2f, 7.2f, creator: 2001, sequence: 1)));
		Assert.True(table.Report(Creation("turret", 5.9f, 7.6f, creator: 2001, sequence: 2)));

		Assert.Equal(2, table.Count);
	}

	[Fact]
	public void Report_AtCap_RefusesNewCreationsButStillUpdatesExisting()
	{
		var table = new PendingEntityReportTable(cap: 2);
		Assert.True(table.Report(Creation("a", 1f, 1f)));
		Assert.True(table.Report(Creation("b", 2f, 2f)));

		Assert.False(table.Report(Creation("c", 3f, 3f)));
		Assert.Equal(2, table.Count);

		Assert.True(table.Report(Creation("a", 1f, 1f, keypadCode: "9999")), "an existing creation always updates — the host only needs its current record");
		Assert.Contains(table.Entries, e => e.Msg.Id == "a" && e.Msg.KeypadCode == "9999");
	}

	[Fact]
	public void Remove_DropsOnlyTheRequestedCreation()
	{
		var table = new PendingEntityReportTable();
		table.Report(Creation("a", 1f, 1f));
		table.Report(Creation("b", 2f, 2f));

		Assert.True(table.Remove(Key("a", 1, 1)));
		Assert.False(table.Remove(Key("a", 1, 1)), "a second remove is a no-op");
		Assert.Equal(1, table.Count);
		Assert.Equal("b", Assert.Single(table.Entries).Msg.Id);
	}

	[Fact]
	public void Remove_MatchesTheFlooredCreationCell()
	{
		var table = new PendingEntityReportTable();
		table.Report(Creation("a", 12.9f, -1.5f)); // cells (12, -2)

		Assert.False(table.Remove(Key("a", 13, -1)), "13.0 is the next cell — a different creation");
		Assert.True(table.Remove(Key("a", 12, -2)));
	}

	[Fact]
	public void RecordAttempt_CountsOnlyWhileTheEntryExists()
	{
		var table = new PendingEntityReportTable();
		table.Report(Creation("a", 1f, 1f));

		Assert.Equal(1, table.RecordAttempt(Key("a", 1, 1)));
		Assert.Equal(2, table.RecordAttempt(Key("a", 1, 1)));
		Assert.Equal(2, Assert.Single(table.Entries).Attempts);

		table.Remove(Key("a", 1, 1));
		Assert.Equal(0, table.RecordAttempt(Key("a", 1, 1)));
	}

	[Fact]
	public void Clear_EmptiesEveryCreation()
	{
		var table = new PendingEntityReportTable();
		table.Report(Creation("a", 1f, 1f));
		table.Report(Creation("b", 2f, 2f));

		table.Clear();

		Assert.Equal(0, table.Count);
		Assert.Empty(table.Entries);
	}

	[Fact]
	public void DefaultCap_MatchesTheHostAcceptedCreationTableBound()
	{
		Assert.Equal(RuntimeEntityRegistry.DefaultCap, PendingEntityReportTable.DefaultCap);
		Assert.Equal(PendingEntityReportTable.DefaultCap, new PendingEntityReportTable().Cap);
	}
}
