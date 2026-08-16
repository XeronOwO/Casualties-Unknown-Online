using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The pure ArmsSwing replay decision for a render proxy: every swing-sequence
/// change replays the clip (rapid swings inside one held IsAttacking window
/// must each be visible), the held flag's rising edge stays the fallback for
/// an old-version sender that never sends a sequence, and the FIRST snapshot
/// only seeds the sequence (a historical SwingSeq from before the clone
/// existed is not a swing).
/// </summary>
public class SwingReplayTests
{
	[Fact]
	public void FirstSnapshot_OnlySeedsTheSequence_NoHistoricalReplay()
	{
		// The owner swung 5 times before this clone joined. The first snapshot
		// arrives with SwingSeq=5 and the flag already false — the clone must
		// not replay a historical swing.
		Assert.False(SwingReplay.ShouldReplay(
			swingSeq: 5, prevSwingSeq: 0, isAttacking: false, prevAttacking: false,
			swingStateSeeded: false));
	}

	[Fact]
	public void FirstSnapshot_MidSwing_StillReplaysOnTheFlagEdge()
	{
		// The clone appears while the owner is visibly mid-swing — the old
		// flag-edge behavior is deliberately kept so the clone shows it.
		Assert.True(SwingReplay.ShouldReplay(
			swingSeq: 5, prevSwingSeq: 0, isAttacking: true, prevAttacking: false,
			swingStateSeeded: false));
	}

	[Fact]
	public void SequenceChange_InsideOneHeldFlagWindow_ReplaysEverySwing()
	{
		// Rapid mining swings: the IsAttacking flag stays true across both, but
		// the rolling sequence changes once per swing.
		Assert.True(SwingReplay.ShouldReplay(
			swingSeq: 2, prevSwingSeq: 1, isAttacking: true, prevAttacking: true,
			swingStateSeeded: true));
	}

	[Fact]
	public void SequenceWrap_IsStillANewSwing()
	{
		Assert.True(SwingReplay.ShouldReplay(
			swingSeq: 0, prevSwingSeq: 255, isAttacking: true, prevAttacking: true,
			swingStateSeeded: true));
	}

	[Fact]
	public void OldVersionSender_FallsBackToTheFlagRisingEdge()
	{
		// An old sender never writes SwingSeq (it stays 0) — the held flag's
		// rising edge is the degraded-but-correct pre-sequence behavior.
		Assert.True(SwingReplay.ShouldReplay(
			swingSeq: 0, prevSwingSeq: 0, isAttacking: true, prevAttacking: false,
			swingStateSeeded: true));
		Assert.False(SwingReplay.ShouldReplay(
			swingSeq: 0, prevSwingSeq: 0, isAttacking: true, prevAttacking: true,
			swingStateSeeded: true));
	}

	[Fact]
	public void NoChange_DoesNotReplay()
	{
		Assert.False(SwingReplay.ShouldReplay(
			swingSeq: 3, prevSwingSeq: 3, isAttacking: false, prevAttacking: false,
			swingStateSeeded: true));
	}
}
