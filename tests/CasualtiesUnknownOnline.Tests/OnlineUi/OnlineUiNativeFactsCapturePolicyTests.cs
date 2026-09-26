using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The retry policy the probe runs on. The game's canvas does not exist when the plugin loads, and the
/// one unknown that needs a RENDERED game (the canvas scale) can be an hour of menu away — so the policy
/// polls fast only while the canvas is missing, slows down once it exists, and then keeps asking until
/// every fact is read or the attempt budget runs out. The cases pin each interval, each terminal state,
/// and the two clock facts this tree has already learned (a wrapped counter must not stall the probe, and
/// it must not disable the bound either).
/// </summary>
public sealed class OnlineUiNativeFactsCapturePolicyTests
{
	[Fact]
	public void TheFirstAttemptIsImmediate()
	{
		var policy = OnlineUiNativeFactsCapturePolicy.Default();

		Assert.True(policy.ShouldAttempt(1_000_000), "the first attempt must not wait for an interval");
		Assert.True(policy.Attempts == 0, $"no attempt has been recorded yet, counted {policy.Attempts}");
		Assert.False(policy.Attached, "nothing has been attached before the first attempt");
	}

	[Fact]
	public void APreCanvasRetryWaitsForTheFastInterval()
	{
		var policy = Policy(fast: 500, slow: 5_000);
		policy.NoteAttempt(NoCanvas(), 1_000_000);

		Assert.False(policy.ShouldAttempt(1_000_100), "an attempt 100 ms later is inside the fast interval");
		Assert.True(policy.ShouldAttempt(1_000_500), "the attempt at the fast interval boundary is due");
	}

	[Fact]
	public void AnAttachedRunRetriesSlowlyInsteadOfHammeringTheAdapter()
	{
		var policy = Policy(fast: 500, slow: 5_000);
		policy.NoteAttempt(Partial(), 1_000_000);

		Assert.True(policy.Attached, "a capture that saw the canvas attaches the run");
		Assert.False(policy.ShouldAttempt(1_000_500), "the fast interval no longer applies once the canvas exists");
		Assert.True(policy.ShouldAttempt(1_005_000), "the slow interval is what paces a run that waits for the game");
	}

	[Fact]
	public void AClockThatWentBackwardsAttemptsAgainInsteadOfStalling()
	{
		var policy = Policy(fast: 500, slow: 5_000);
		policy.NoteAttempt(Partial(), 1_000_000);

		// Environment.TickCount wraps: an earlier stamp must not read as "not due for 24 days"
		Assert.True(policy.ShouldAttempt(900_000), "a wrapped clock must start a fresh interval");
	}

	[Fact]
	public void ACompleteReadingFinishesTheRunImmediately()
	{
		var policy = Policy();

		var outcome = policy.NoteAttempt(Complete(), 1_000_000);

		Assert.True(outcome == OnlineUiNativeFactsOutcome.Complete, $"a complete reading is the end, got {outcome}");
		Assert.False(policy.ShouldAttempt(9_000_000), "a finished run must never ask again");
	}

	[Fact]
	public void AReadingThatIsNotCompleteKeepsAsking()
	{
		var policy = Policy();

		Assert.True(policy.NoteAttempt(Partial(), 1_000_000) == OnlineUiNativeFactsOutcome.Pending);
		Assert.True(policy.ShouldAttempt(1_005_000), "the canvas was found but the run-only facts are not readable yet");
	}

	[Fact]
	public void AnAttachedRunOutlastsTheSurfaceDeadline()
	{
		var policy = Policy(fast: 500, slow: 5_000, surface: 5_000);
		policy.NoteAttempt(Partial(), 1_000_000);

		// The game may sit in a menu far longer than the surface window: a run that has SEEN the canvas
		// must keep asking — an earlier design ended here and threw the reading away on an ordinary session.
		var later = 1_000_000 + (30 * 60_000);
		var outcome = policy.NoteAttempt(Partial(), later);

		Assert.True(outcome == OnlineUiNativeFactsOutcome.Pending, $"an attached run must outlast the surface window, got {outcome}");
		Assert.True(policy.ShouldAttempt(later + 5_000), "and it must still be due on the slow interval");
	}

	[Fact]
	public void ACanvasThatNeverAppearedFinishesAsSurfaceMissing()
	{
		var policy = Policy(fast: 500, slow: 5_000, surface: 2_000);
		policy.NoteAttempt(NoCanvas(), 1_000_000);

		var outcome = policy.NoteAttempt(NoCanvas(), 1_002_000);

		Assert.True(
			outcome == OnlineUiNativeFactsOutcome.SurfaceMissing,
			$"no canvas at all is its own outcome, got {outcome}");
		Assert.False(policy.ShouldAttempt(1_003_000), "a finished run must never ask again");
	}

	[Fact]
	public void TheAttemptBudgetFinishesTheRun()
	{
		var policy = Policy(fast: 0, slow: 0, surface: 600_000, max: 3);
		policy.NoteAttempt(Partial(), 1_000_000);
		policy.NoteAttempt(Partial(), 1_000_001);

		var outcome = policy.NoteAttempt(Partial(), 1_000_002);

		Assert.True(outcome == OnlineUiNativeFactsOutcome.Partial, $"the budget ends the run, got {outcome}");
		Assert.True(policy.Attempts == 3, $"every attempt counts, counted {policy.Attempts}");
	}

	[Fact]
	public void AFinishedRunKeepsItsOutcomeAndItsAttemptCount()
	{
		var policy = Policy();
		policy.NoteAttempt(Complete(), 1_000_000);

		var outcome = policy.NoteAttempt(NoCanvas(), 1_500_000);

		Assert.True(outcome == OnlineUiNativeFactsOutcome.Complete, $"the first terminal state wins, got {outcome}");
		Assert.True(policy.Attempts == 1, $"a finished run records no further attempt, counted {policy.Attempts}");
	}

	[Fact]
	public void TheDefaultBudgetIsBoundedOnEveryAxis()
	{
		Assert.True(
			OnlineUiNativeFactsCapturePolicy.DefaultFastIntervalMs >= 100
				&& OnlineUiNativeFactsCapturePolicy.DefaultFastIntervalMs <= 1_000,
			$"the polling interval must keep the startup retry off the per-frame path, got {OnlineUiNativeFactsCapturePolicy.DefaultFastIntervalMs}");
		Assert.True(
			OnlineUiNativeFactsCapturePolicy.DefaultSlowIntervalMs >= 1_000
				&& OnlineUiNativeFactsCapturePolicy.DefaultSlowIntervalMs <= 60_000,
			$"the waiting interval must be long enough to cost a session almost nothing, got {OnlineUiNativeFactsCapturePolicy.DefaultSlowIntervalMs}");
		Assert.True(
			OnlineUiNativeFactsCapturePolicy.DefaultSurfaceDeadlineMs >= 10_000
				&& OnlineUiNativeFactsCapturePolicy.DefaultSurfaceDeadlineMs <= 900_000,
			$"a game that never reaches a menu must be given up on inside a bounded window, got {OnlineUiNativeFactsCapturePolicy.DefaultSurfaceDeadlineMs}");
		Assert.True(
			OnlineUiNativeFactsCapturePolicy.DefaultMaxAttempts >= 100
				&& OnlineUiNativeFactsCapturePolicy.DefaultMaxAttempts <= 100_000,
			$"the attempt budget is the hard bound for a canvas that never becomes readable, got {OnlineUiNativeFactsCapturePolicy.DefaultMaxAttempts}");
	}

	private static OnlineUiNativeFactsCapturePolicy Policy(
		int fast = 500,
		int slow = 5_000,
		int surface = 300_000,
		int max = 2_000) => new(fast, slow, surface, max);

	private static OnlineUiNativeFacts Complete() => new(
		"Canvas/SettingsMenu",
		[Image("UIPanel")],
		[Image("UIButton")],
		new OnlineUiNativeTextStyle("Special/GameSettingDropdown", "Row/Label", "SDF", 14f, new OnlineUiNativeRgba(1f, 1f, 1f, 1f), 1),
		1f,
		null);

	private static OnlineUiNativeFacts Partial() => new("Canvas/SettingsMenu", [Image("UIPanel")], [], null, null, null);

	private static OnlineUiNativeFacts NoCanvas() => OnlineUiNativeFacts.Unavailable("the game has no main canvas yet");

	private static OnlineUiNativeImageStyle Image(string sprite) => new(
		"Special/GameSettingDropdown",
		"CUO Online UI Native Host/Row",
		sprite,
		"Sliced",
		1f,
		0f,
		0f,
		0f,
		0f,
		new OnlineUiNativeRgba(0.1f, 0.2f, 0.3f, 1f),
		1);
}
