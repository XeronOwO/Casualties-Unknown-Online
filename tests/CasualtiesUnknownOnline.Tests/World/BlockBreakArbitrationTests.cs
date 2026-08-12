using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The block-break first-writer-wins arbitration (BlockBreakArbitration): the
/// host records each guest's APPLIED air-write, the drops-carrying break
/// report consumes it exactly once — the only fact that distinguishes the
/// first breaker from a second (GetBlock cannot: the block is air for both).
/// Time and coordinates are explicit inputs.
/// </summary>
public class BlockBreakArbitrationTests
{
	private const ulong GuestA = 2001;
	private const ulong GuestB = 2002;
	private const int CellX = 5;
	private const int CellY = -3;
	private const float Ttl = 3f;

	[Fact]
	public void AirWrite_ThenBreak_AcceptedOnce()
	{
		var arbitration = new BlockBreakArbitration();
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 0f);

		Assert.True(arbitration.TryAccept(GuestA, CellX, CellY), "the applied air-write proves first-writer");
		Assert.False(arbitration.TryAccept(GuestA, CellX, CellY), "one-shot — a second break report is refused");
	}

	[Fact]
	public void Break_WithoutAirWrite_Refused()
	{
		var arbitration = new BlockBreakArbitration();

		Assert.False(arbitration.TryAccept(GuestA, CellX, CellY));
	}

	[Fact]
	public void OtherSender_NotAccepted_BySomeoneElsesRecord()
	{
		var arbitration = new BlockBreakArbitration();
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 0f);

		Assert.False(arbitration.TryAccept(GuestB, CellX, CellY), "the record is per sender");
		Assert.True(arbitration.TryAccept(GuestA, CellX, CellY), "the real breaker still consumes its own record");
	}

	[Fact]
	public void OtherCell_NotAccepted_ByAdjacentRecord()
	{
		var arbitration = new BlockBreakArbitration();
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 0f);

		Assert.False(arbitration.TryAccept(GuestA, CellX, CellY + 1), "the record is per cell");
	}

	[Fact]
	public void RepeatedAirWrite_StillOneBreakToAccept()
	{
		var arbitration = new BlockBreakArbitration();
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 0f);
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 1f); // a repeat air-write overwrites

		Assert.True(arbitration.TryAccept(GuestA, CellX, CellY));
		Assert.False(arbitration.TryAccept(GuestA, CellX, CellY));
	}

	[Fact]
	public void PurgeStale_RemovesExpired_KeepsFresh()
	{
		var arbitration = new BlockBreakArbitration();
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 0f);
		arbitration.RecordAppliedAirWrite(GuestB, CellX + 1, CellY, now: 2f);

		arbitration.PurgeStale(now: 3f, Ttl); // A: 3 > 3? no — the boundary is exclusive
		Assert.True(arbitration.TryAccept(GuestA, CellX, CellY));

		arbitration.PurgeStale(now: 3.01f, Ttl); // A: 3.01 > 3 — expired; B: 1.01 < 3 — fresh
		Assert.False(arbitration.TryAccept(GuestA, CellX, CellY));
		Assert.True(arbitration.TryAccept(GuestB, CellX + 1, CellY));
	}

	[Fact]
	public void PurgeStale_EmptyTable_NoOp()
	{
		var arbitration = new BlockBreakArbitration();

		arbitration.PurgeStale(now: 100f, Ttl);

		Assert.Equal(0, arbitration.Count);
	}

	[Fact]
	public void FullSequence_FirstWriterWins_SecondRefused()
	{
		var arbitration = new BlockBreakArbitration();

		// GuestA breaks the cell first (its air-write applied and consumed).
		arbitration.RecordAppliedAirWrite(GuestA, CellX, CellY, now: 0f);
		Assert.True(arbitration.TryAccept(GuestA, CellX, CellY));

		// GuestB's break of the SAME cell arrives with its own record? It has
		// none (its air-write was refused as already-broken) — refused.
		Assert.False(arbitration.TryAccept(GuestB, CellX, CellY));
	}
}
