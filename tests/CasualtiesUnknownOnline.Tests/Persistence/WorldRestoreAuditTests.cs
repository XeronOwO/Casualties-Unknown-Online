using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The restore audit: the live-world half of a restore happens at the
/// world-entry seam, long after the Continue click returned, so the click's
/// caller can only learn what the live world took through this object. The suite
/// pins its lifecycle — a pending restore, a completed or incomplete write, and a
/// restore that never reached the seam.
/// </summary>
public class WorldRestoreAuditTests
{
	[Fact]
	public void Begin_ExpectsTheLiveWorldWriteAndClearsAStaleReport()
	{
		var audit = new WorldRestoreAudit();
		audit.BeginRestore("w-1");
		audit.LiveWriteFinished(complete: true, refused: [], summary: "took everything");
		Assert.NotNull(audit.Last);

		audit.BeginRestore("w-2");

		Assert.True(audit.AwaitingLiveWrite);
		Assert.Equal("w-2", audit.PendingWorldId);
		Assert.Null(audit.Last);
	}

	[Fact]
	public void LiveWriteFinished_Complete_ReportsTheWorld()
	{
		var audit = new WorldRestoreAudit();
		var reported = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reported.Add;
		audit.BeginRestore("w-1");

		audit.LiveWriteFinished(complete: true, refused: [], summary: "12 block-state row(s) written");

		var report = Assert.Single(reported);
		Assert.Equal("w-1", report.WorldId);
		Assert.True(report.Complete);
		Assert.Empty(report.Refused);
		Assert.False(audit.AwaitingLiveWrite);
		Assert.Same(report, audit.Last);
	}

	[Fact]
	public void LiveWriteFinished_Incomplete_CarriesEveryRefusedClass()
	{
		var audit = new WorldRestoreAudit();
		var reported = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reported.Add;
		audit.BeginRestore("w-9");

		audit.LiveWriteFinished(
			complete: false,
			refused: ["2 partial-damage row(s)", "1 keypad code(s)"],
			summary: "the live world did not take 2 partial-damage row(s), 1 keypad code(s)");

		var report = Assert.Single(reported);
		Assert.False(report.Complete);
		Assert.Equal(2, report.Refused.Count);
		Assert.Contains("did not take", report.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void AbandonRestore_DropsTheExpectationWithoutInventingAReport()
	{
		var audit = new WorldRestoreAudit();
		var reports = 0;
		audit.Reported += _ => reports++;
		audit.BeginRestore("w-3");

		audit.AbandonRestore();

		// A restore that never happened is not a restore that succeeded.
		Assert.False(audit.AwaitingLiveWrite);
		Assert.Equal(string.Empty, audit.PendingWorldId);
		Assert.Null(audit.Last);
		Assert.Equal(0, reports);
	}

	[Fact]
	public void LiveWriteFinished_WithoutABegin_StillReportsTheWrite()
	{
		// The replay is the only producer; a restore whose Begin was skipped (a test
		// host, or a future path that arms the handover without the click) must not
		// lose the account of what the live world refused.
		var audit = new WorldRestoreAudit();
		var reported = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reported.Add;

		audit.LiveWriteFinished(complete: false, refused: ["1 block-state row(s)"], summary: "did not take 1 block-state row(s)");

		Assert.Single(reported);
		Assert.Equal(string.Empty, Assert.Single(reported).WorldId);
	}
}
