using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The block-break first-writer-wins arbitration (BlockBreakArbitration): the
/// host records each guest's APPLIED air-write, the drops-carrying break
/// report consumes it exactly once — the only fact that distinguishes the
/// first breaker from a second (GetBlock cannot: the block is air for both).
/// Since the break-drop recovery landed, the same sender may re-report a break
/// the host already accepted (its relay — the acknowledgement — can be the lost
/// message), so a repeat is acknowledged idempotently while a DIFFERENT sender
/// stays refused. Time and coordinates are explicit inputs.
/// </summary>
public class BlockBreakArbitrationTests
{
	private const ulong GuestA = 2001;
	private const ulong GuestB = 2002;
	private const int CellX = 5;
	private const int CellY = -3;
	private const float Ttl = 3f;
	private const float AcceptedTtl = 90f;

	[Fact]
	public void AirWrite_ThenBreak_AcceptedOnce()
	{
		var arbitration = new BlockBreakArbitration();
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 0f);

		Assert.Equal(Verdict.Fresh, arbitration.TryAccept(GuestA, CellX, CellY));
		arbitration.RecordAccepted(GuestA, CellX, CellY, now: 0.5f);
		Assert.Equal(Verdict.Repeat, arbitration.TryAccept(GuestA, CellX, CellY));
	}

	[Fact]
	public void Break_WithoutAirWrite_Refused()
	{
		var arbitration = new BlockBreakArbitration();

		Assert.Equal(Verdict.Refused, arbitration.TryAccept(GuestA, CellX, CellY));
	}

	[Fact]
	public void Break_WithoutAnAirWriteRecord_IsRefused_EvenOnAStandingBlock()
	{
		// A report naming a cell the host still holds looks attributable (no earlier
		// break can have taken a cell this side still holds), but the record it would
		// create is layer-relative while the report is not: after a descent the same
		// report names a freshly generated block. It is therefore refused — the
		// pre-change behaviour — and no attribution is left behind.
		var arbitration = new BlockBreakArbitration();

		Assert.Equal(Verdict.Refused, arbitration.TryAccept(GuestA, CellX, CellY));
		Assert.Equal(0, arbitration.AcceptedCount);
		Assert.Equal(0, arbitration.Count);
	}

	[Fact]
	public void AnotherSender_CannotClaimAnAcceptedCell_EvenOnAStandingBlock()
	{
		// First-writer-wins: the cell already belongs to GuestA, so GuestB's report
		// cannot claim it however the host's own copy of the cell looks.
		var arbitration = new BlockBreakArbitration();
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 0f);
		Assert.Equal(Verdict.Fresh, arbitration.TryAccept(GuestA, CellX, CellY));
		arbitration.RecordAccepted(GuestA, CellX, CellY, now: 0f);

		Assert.Equal(Verdict.Refused, arbitration.TryAccept(GuestB, CellX, CellY));
	}

	[Fact]
	public void OtherSender_NotAccepted_BySomeoneElsesRecord()
	{
		var arbitration = new BlockBreakArbitration();
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 0f);

		Assert.Equal(Verdict.Refused, arbitration.TryAccept(GuestB, CellX, CellY));
		Assert.Equal(Verdict.Fresh, arbitration.TryAccept(GuestA, CellX, CellY));
	}

	[Fact]
	public void OtherSender_Refused_AfterTheBreakWasAccepted()
	{
		// The second breaker's air-write never applied (the cell was already air),
		// so it has no record — and the accepted record belongs to the first
		// breaker, never to it.
		var arbitration = new BlockBreakArbitration();
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 0f);
		Assert.Equal(Verdict.Fresh, arbitration.TryAccept(GuestA, CellX, CellY));
		arbitration.RecordAccepted(GuestA, CellX, CellY, now: 0f);

		Assert.Equal(Verdict.Refused, arbitration.TryAccept(GuestB, CellX, CellY));
	}

	[Fact]
	public void OtherCell_NotAccepted_ByAdjacentRecord()
	{
		var arbitration = new BlockBreakArbitration();
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 0f);

		Assert.Equal(Verdict.Refused, arbitration.TryAccept(GuestA, CellX, CellY + 1));
	}

	[Fact]
	public void RepeatedAirWrite_StillOneBreakToAccept()
	{
		var arbitration = new BlockBreakArbitration();
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 0f);
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 1f); // a repeat air-write overwrites

		Assert.Equal(Verdict.Fresh, arbitration.TryAccept(GuestA, CellX, CellY));
		arbitration.RecordAccepted(GuestA, CellX, CellY, now: 1.1f);
		Assert.Equal(Verdict.Repeat, arbitration.TryAccept(GuestA, CellX, CellY));
	}

	[Fact]
	public void RepeatedReport_InsideTheAcceptedWindow_IsARepeat()
	{
		var arbitration = new BlockBreakArbitration();
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 0f);
		Assert.Equal(Verdict.Fresh, arbitration.TryAccept(GuestA, CellX, CellY));
		arbitration.RecordAccepted(GuestA, CellX, CellY, now: 0f);

		// The guest's 60 s fallback re-sends the break (its relay was the lost
		// message): the accepted record still holds, so the host re-relays instead
		// of refusing the drops the breaker still holds locally.
		Assert.Equal(Verdict.Repeat, arbitration.TryAccept(GuestA, CellX, CellY));

		// Past the window the record is gone and the report is refused like any
		// unattributed break: the host cannot prove whose break this is anymore.
		arbitration.PurgeStale(now: 100f, ttl: Ttl, acceptedTtl: AcceptedTtl);
		Assert.Equal(Verdict.Refused, arbitration.TryAccept(GuestA, CellX, CellY));
	}

	[Fact]
	public void RepeatedReport_RefreshesTheWindow_SoAnAnsweredReportKeepsAttribution()
	{
		// A guest that never sees the relay keeps re-reporting every 60 s. Each
		// accepted repeat re-commits the record, so the window measures time since
		// the LAST report rather than since the first — otherwise the third window
		// would be attributed by nobody and a still-valid drop would be destroyed.
		var arbitration = new BlockBreakArbitration();
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 0f);
		Assert.Equal(Verdict.Fresh, arbitration.TryAccept(GuestA, CellX, CellY));
		arbitration.RecordAccepted(GuestA, CellX, CellY, now: 0f);

		arbitration.PurgeStale(now: 65f, ttl: Ttl, acceptedTtl: AcceptedTtl);
		Assert.Equal(Verdict.Repeat, arbitration.TryAccept(GuestA, CellX, CellY));
		arbitration.RecordAccepted(GuestA, CellX, CellY, now: 65f); // the repeat refreshes

		// 120 s after the ORIGINAL break, the refreshed record is still inside its
		// own window — the fallback's next re-report is attributed, not refused.
		arbitration.PurgeStale(now: 120f, ttl: Ttl, acceptedTtl: AcceptedTtl);
		Assert.Equal(Verdict.Repeat, arbitration.TryAccept(GuestA, CellX, CellY));
	}

	[Fact]
	public void PurgeStale_RemovesExpired_KeepsFresh()
	{
		var arbitration = new BlockBreakArbitration();
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 0f);
		arbitration.RecordAppliedAirWrite(GuestB, CellX + 1, CellY, now: 2f);

		arbitration.PurgeStale(now: 3f, ttl: Ttl, acceptedTtl: AcceptedTtl); // A: 3 > 3? no — the boundary is exclusive
		Assert.Equal(Verdict.Fresh, arbitration.TryAccept(GuestA, CellX, CellY));
		arbitration.RecordAccepted(GuestA, CellX, CellY, now: 3f);

		arbitration.PurgeStale(now: 6.01f, ttl: Ttl, acceptedTtl: AcceptedTtl); // A's accepted record (now 3) is well inside its window; B's air-write is gone with it
		Assert.Equal(Verdict.Refused, arbitration.TryAccept(GuestB, CellX + 1, CellY));
		Assert.Equal(Verdict.Repeat, arbitration.TryAccept(GuestA, CellX, CellY));
	}

	[Fact]
	public void PurgeStale_KeepsTheAcceptedRecordForTheReReportWindow()
	{
		var arbitration = new BlockBreakArbitration();
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 0f);
		Assert.Equal(Verdict.Fresh, arbitration.TryAccept(GuestA, CellX, CellY));
		arbitration.RecordAccepted(GuestA, CellX, CellY, now: 0f);

		arbitration.PurgeStale(now: 61f, ttl: Ttl, acceptedTtl: AcceptedTtl);

		Assert.Equal(1, arbitration.AcceptedCount);
		Assert.Equal(Verdict.Repeat, arbitration.TryAccept(GuestA, CellX, CellY));
	}

	[Fact]
	public void PurgeStale_EmptyTable_NoOp()
	{
		var arbitration = new BlockBreakArbitration();

		arbitration.PurgeStale(now: 100f, ttl: Ttl, acceptedTtl: AcceptedTtl);

		Assert.Equal(0, arbitration.Count);
		Assert.Equal(0, arbitration.AcceptedCount);
	}

	[Fact]
	public void FullSequence_FirstWriterWins_SecondRefused()
	{
		var arbitration = new BlockBreakArbitration();

		// GuestA breaks the cell first (its air-write applied and consumed).
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 0f);
		Assert.Equal(Verdict.Fresh, arbitration.TryAccept(GuestA, CellX, CellY));
		arbitration.RecordAccepted(GuestA, CellX, CellY, now: 0f);

		// GuestB's break of the SAME cell arrives with its own record? It has
		// none (its air-write was refused as already-broken) — refused.
		Assert.Equal(Verdict.Refused, arbitration.TryAccept(GuestB, CellX, CellY));
	}

	[Fact]
	public void Reset_DropsEveryRecord_SoAStaleReportCannotClaimTheNewLayer()
	{
		// A world/layer boundary regenerates the terrain: the cell keys are
		// layer-relative, and an accepted record kept across it let a pending
		// re-report of the OLD layer's break match a freshly generated block
		// (no air-write record is needed for a repeat) and be acknowledged against it.
		var arbitration = new BlockBreakArbitration();
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 0f);
		Assert.Equal(Verdict.Refused, arbitration.TryAccept(GuestB, CellX, CellY));
		arbitration.RecordAccepted(GuestB, CellX, CellY, now: 0f);
		arbitration.RecordAppliedAirWrite(GuestA, CellX + 1, CellY, now: 0f);

		arbitration.Reset();

		Assert.Equal(0, arbitration.Count);
		Assert.Equal(0, arbitration.AcceptedCount);
		Assert.Equal(Verdict.Refused, arbitration.TryAccept(GuestB, CellX, CellY));
		Assert.Equal(Verdict.Refused, arbitration.TryAccept(GuestA, CellX + 1, CellY));
	}

	[Fact]
	public void AVerdictWithoutACommit_LeavesNoAttribution()
	{
		// The verdict is only a decision; the adapter commits it after the break is
		// real. An uncommitted verdict must not refuse a genuine second breaker.
		var arbitration = new BlockBreakArbitration();

		Assert.Equal(Verdict.Refused, arbitration.TryAccept(GuestA, CellX, CellY));
		Assert.Equal(0, arbitration.AcceptedCount);

		Assert.Equal(Verdict.Refused, arbitration.TryAccept(GuestB, CellX, CellY));
	}
}
