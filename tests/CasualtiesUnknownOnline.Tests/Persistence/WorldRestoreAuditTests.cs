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

	[Fact]
	public void ASecondExpectedHalf_HoldsTheReportUntilItArrives()
	{
		// A mid-run cut owes TWO live-world halves: the world facts at the
		// world-entry seam and the item reconcile at the generation's publish. The
		// first half is not the restore's outcome — reporting it would tell the
		// player a restore succeeded while half of it has not been written yet.
		var audit = new WorldRestoreAudit();
		var reported = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reported.Add;
		audit.BeginRestore("w-7", expectedContributions: 2);

		audit.LiveWriteFinished(complete: true, refused: [], summary: "the live world took every restored fact");

		Assert.Empty(reported);
		Assert.True(audit.AwaitingLiveWrite);
		Assert.Equal(1, audit.Contributions);
		Assert.Equal(2, audit.ExpectedContributions);

		audit.LiveWriteFinished(complete: true, refused: [], summary: "the live world took the restored item set (3 entries)");

		var report = Assert.Single(reported);
		Assert.True(report.Complete);
		Assert.Equal("w-7", report.WorldId);
		Assert.Contains("item set", report.Summary, StringComparison.Ordinal);
		Assert.False(audit.AwaitingLiveWrite);
		Assert.Same(report, audit.Last);
	}

	[Fact]
	public void ASecondHalfThatWasRefused_MakesTheWholeRestoreIncomplete()
	{
		var audit = new WorldRestoreAudit();
		var reported = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reported.Add;
		audit.BeginRestore("w-8", expectedContributions: 2);

		audit.LiveWriteFinished(complete: true, refused: [], summary: "the live world took every restored fact");
		audit.LiveWriteFinished(complete: false, refused: ["2 restored item(s)"], summary: "the live world did not take 2 restored item(s)");

		var report = Assert.Single(reported);
		Assert.False(report.Complete);
		Assert.Equal("2 restored item(s)", Assert.Single(report.Refused));
		Assert.Contains("every restored fact", report.Summary, StringComparison.Ordinal);
		Assert.Contains("did not take 2 restored item(s)", report.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void LiveWriteAbandoned_AccountsForTheMissingHalf()
	{
		// A half that will never arrive (a cancelled reconcile, the session ending)
		// is accounted for, not waited on: the restore's account must not stay
		// silently armoured forever.
		var audit = new WorldRestoreAudit();
		var reported = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reported.Add;
		audit.BeginRestore("w-10", expectedContributions: 2);
		audit.LiveWriteFinished(complete: true, refused: [], summary: "the live world took every restored fact");

		audit.LiveWriteAbandoned("the session ended before the generation reconcile ran");

		var report = Assert.Single(reported);
		Assert.False(report.Complete);
		Assert.Contains("generation reconcile", Assert.Single(report.Refused), StringComparison.Ordinal);
		Assert.False(audit.AwaitingLiveWrite);
	}

	[Fact]
	public void LiveWriteAbandoned_WithoutARestoreInFlight_IsANoOp()
	{
		// A layer-end cut cancels the item expectation it never armed; that
		// cancellation must not invent a restore report.
		var audit = new WorldRestoreAudit();
		var reports = 0;
		audit.Reported += _ => reports++;

		audit.LiveWriteAbandoned("a layer-end cut never reconciles its items");

		Assert.Equal(0, reports);
		Assert.Null(audit.Last);
	}

	[Fact]
	public void AThirdExpectedHalf_HoldsTheReportUntilTheItemReconcileArrives()
	{
		// A mid-run cut owes three live-world halves: the world facts and the
		// world-entity facts at the world-entry seam, then the item reconcile one frame
		// later. The report waits for the LAST one.
		var audit = new WorldRestoreAudit();
		var reported = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reported.Add;
		audit.BeginRestore("w-11", expectedContributions: WorldRestoreApplier.LiveWorldHalves(worldEntityHalfArmed: true, itemReconcileArmed: true));

		audit.LiveWriteFinished(complete: true, refused: [], summary: "the live world took every restored fact");
		audit.LiveWriteFinished(complete: true, refused: [], summary: "the live world took every restored world-entity fact (3 row(s))");

		Assert.Empty(reported);
		Assert.True(audit.AwaitingLiveWrite);
		Assert.Equal(2, audit.Contributions);

		audit.LiveWriteFinished(complete: true, refused: [], summary: "the live world took the restored item set (1 entry)");

		var report = Assert.Single(reported);
		Assert.True(report.Complete);
		Assert.Contains("world-entity", report.Summary, StringComparison.Ordinal);
		Assert.Contains("item set", report.Summary, StringComparison.Ordinal);
		Assert.False(audit.AwaitingLiveWrite);
	}

	[Fact]
	public void LiveWorldHalves_CountTheWritersThatAreActuallyArmed()
	{
		// The count is a CONTRACT between the restore applier and this audit: the report
		// is raised when the contribution count reaches the expectation, so a half nobody
		// will report leaves the restore awaiting forever, while a count that is too low
		// raises the report before the last writer ran and then raises a SECOND one when
		// that writer reports. It therefore follows the writers that are ARMED at the
		// moment the click returns — never the cut kind — and the world-fact half always
		// reports (an armed restore with no facts at all reports "carried nothing").
		Assert.Equal(3, WorldRestoreApplier.LiveWorldHalves(worldEntityHalfArmed: true, itemReconcileArmed: true));
		Assert.Equal(2, WorldRestoreApplier.LiveWorldHalves(worldEntityHalfArmed: true, itemReconcileArmed: false));
		Assert.Equal(2, WorldRestoreApplier.LiveWorldHalves(worldEntityHalfArmed: false, itemReconcileArmed: true));
		Assert.Equal(1, WorldRestoreApplier.LiveWorldHalves(worldEntityHalfArmed: false, itemReconcileArmed: false));
	}

	[Fact]
	public void LiveWriteFinished_AfterTheReportWasRaised_IsIgnored()
	{
		// A restore reports ONCE. The world-entry seam runs the replay once per
		// generation, so a normal generation after a completed restore reports a no-op;
		// accepting it would raise a second report for a world whose restore already
		// finished, and the player would be told about a restore that is not happening.
		var audit = new WorldRestoreAudit();
		var reported = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reported.Add;
		audit.BeginRestore("w-12");

		audit.LiveWriteFinished(complete: true, refused: [], summary: "the live world took every restored fact");
		audit.LiveWriteFinished(complete: true, refused: [], summary: "a later generation reported a no-op");

		var report = Assert.Single(reported);
		Assert.True(report.Complete);
		Assert.Equal(1, audit.Contributions);

		// A NEW restore starts a new account: the ignore rule is per restore.
		audit.BeginRestore("w-13");
		audit.LiveWriteFinished(complete: true, refused: [], summary: "the live world took the second restore");
		Assert.Equal(2, reported.Count);
		Assert.Equal("w-13", reported[1].WorldId);
	}

	[Fact]
	public void LiveWriteFinished_AfterAnAbandonedRestore_IsIgnored()
	{
		// An abandoned restore is CLOSED, not merely un-awaited: the failure paths that
		// abandon one (a baseline that could not be published, a superseded attempt) can
		// leave a handover armed somewhere, and a write that later reaches the seam for
		// that dead attempt must not raise a report about a restore that never happened.
		var audit = new WorldRestoreAudit();
		var reports = 0;
		audit.Reported += _ => reports++;
		audit.BeginRestore("w-14");
		audit.AbandonRestore();

		audit.LiveWriteFinished(complete: true, refused: [], summary: "a stale write reached the seam after the attempt was abandoned");

		Assert.Equal(0, reports);
		Assert.Null(audit.Last);
		Assert.False(audit.AwaitingLiveWrite);

		// The next restore opens a fresh account.
		audit.BeginRestore("w-15");
		audit.LiveWriteFinished(complete: true, refused: [], summary: "the next restore's write");
		Assert.Equal(1, reports);
		Assert.Equal("w-15", audit.Last!.WorldId);
	}

	[Fact]
	public void AbandonRestore_WithNoRestoreInFlight_KeepsTheNoBeginPathAlive()
	{
		// A new run calls AbandonRestore whether or not a restore was applied (the run
		// owns the next generation either way). With NOTHING in flight there is nothing to
		// abandon, and closing the account there would silently disable the documented
		// diagnostic path where a write with no Begin still reports what the live world
		// refused.
		var audit = new WorldRestoreAudit();
		var reported = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reported.Add;

		audit.AbandonRestore();
		audit.LiveWriteFinished(complete: false, refused: ["1 block-state row(s)"], summary: "did not take 1 block-state row(s)");

		var report = Assert.Single(reported);
		Assert.False(report.Complete);
	}

	[Fact]
	public void AbandonRestore_ClearsThePreviousAccountToo()
	{
		// A completed restore's report must never be read as the next (abandoned)
		// attempt's outcome: Last is part of the restore in flight.
		var audit = new WorldRestoreAudit();
		audit.BeginRestore("w-1");
		audit.LiveWriteFinished(complete: true, refused: [], summary: "took everything");
		Assert.NotNull(audit.Last);

		audit.BeginRestore("w-2");
		audit.AbandonRestore();

		Assert.Null(audit.Last);
		Assert.False(audit.AwaitingLiveWrite);
	}
}
