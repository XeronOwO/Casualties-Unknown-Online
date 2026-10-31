using CasualtiesUnknownOnline.GameAdapter.Run;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The host-descent follow's transition table and its bounded waits. Behaviour family: what a member's client believes about a descent it is following — the
/// settle edge, the early release, and the two ways a follow gives up. The coordinator keeps the scene reads and the phase mapping; this pins the decisions.
/// </summary>
[Trait("Category", "Unit")]
public class HostDescentFollowTests
{
	[Fact]
	public void AFreshFollow_IsNotActiveAndStartsNothing()
	{
		var follow = new HostDescentFollow();

		Assert.Equal(DescentPhase.None, follow.Phase);
		Assert.False(follow.IsActive);
		Assert.False(follow.IsGenerationInFlight);
		Assert.False(follow.HostPending);
	}

	[Fact]
	public void TheEdgeThenTheLaunchThenTheSettle_IsTheGuestPath()
	{
		var follow = new HostDescentFollow();
		follow.Accept(1_000);

		Assert.Equal(DescentPhase.Accepted, follow.Phase);
		Assert.True(follow.IsGenerationInFlight);
		Assert.True(follow.CanBeReleasedEarly);
		Assert.False(follow.HasLaunched);

		follow.Launched();
		Assert.Equal(DescentPhase.Launched, follow.Phase);
		Assert.True(follow.HasLaunched);

		follow.WaitForReady();
		Assert.Equal(DescentPhase.WaitingReady, follow.Phase);
		Assert.False(follow.IsGenerationInFlight);

		follow.End();
		Assert.Equal(DescentPhase.None, follow.Phase);
	}

	[Fact]
	public void ALaunchWithoutAnAcceptedEdge_ChangesNothing()
	{
		var follow = new HostDescentFollow();

		follow.Launched();

		Assert.Equal(DescentPhase.None, follow.Phase);
	}

	[Fact]
	public void AnEarlyRelease_KeepsTheGenerationInFlightUntilTheSettle()
	{
		var follow = new HostDescentFollow();
		follow.Accept(0);
		follow.Launched();
		follow.ReleaseEarly();

		Assert.Equal(DescentPhase.ReleasedEarly, follow.Phase);
		Assert.True(follow.IsGenerationInFlight);
		Assert.False(follow.CanBeReleasedEarly);
	}

	[Fact]
	public void TheAbortLadder_NamesTheWorldFirst_ThenTheHold_ThenTheSettle()
	{
		var follow = new HostDescentFollow();
		follow.Accept(0);

		// The world gone is an abort on its own, whatever the clocks say.
		Assert.True(follow.IsAbortDue(hasWorld: false, nowMs: 0));
		Assert.Equal("the world was left (a leave or a scene change) mid-descent", follow.AbortReason(hasWorld: false));

		// In a live world the hold window bounds the wait for the baseline.
		Assert.False(follow.IsAbortDue(hasWorld: true, nowMs: LayerDescentPolicy.HoldTimeoutMs));
		Assert.True(follow.IsAbortDue(hasWorld: true, nowMs: LayerDescentPolicy.HoldTimeoutMs + 1));
		Assert.Equal("the host's baseline for the new layer never arrived", follow.AbortReason(hasWorld: true));

		// After the launch the settle window takes over and is much longer.
		follow.Launched();
		Assert.False(follow.IsAbortDue(hasWorld: true, nowMs: LayerDescentPolicy.HoldTimeoutMs + 1));
		Assert.True(follow.IsAbortDue(hasWorld: true, nowMs: LayerDescentPolicy.SettleTimeoutMs + 1));
		Assert.Equal("the new layer's generation never settled", follow.AbortReason(hasWorld: true));
	}

	[Fact]
	public void AWaitingFollow_IsNotAbortedByTheClocks()
	{
		var follow = new HostDescentFollow();
		follow.Accept(0);
		follow.Launched();
		follow.WaitForReady();

		Assert.False(follow.IsAbortDue(hasWorld: true, nowMs: LayerDescentPolicy.SettleTimeoutMs + 1));
		Assert.True(follow.IsAbortDue(hasWorld: false, nowMs: 0));
	}

	[Fact]
	public void TheSettleDecision_AnswersOnlyWhenTheGenerationFinished()
	{
		var follow = new HostDescentFollow();
		follow.Accept(0);
		follow.Launched();

		// Still generating: no decision yet, either flag.
		Assert.False(follow.TryDecideSettle(generating: true, regenerating: false, liveWorld: true, out _));
		Assert.False(follow.TryDecideSettle(generating: false, regenerating: true, liveWorld: true, out _));
		Assert.False(follow.TryDecideSettle(generating: false, regenerating: false, liveWorld: false, out _));

		// Settled with no release in hand: the caller waits for the host on the loading screen.
		Assert.True(follow.TryDecideSettle(generating: false, regenerating: false, liveWorld: true, out var startsPlaying));
		Assert.False(startsPlaying);

		// A release that arrived while the generation was running ends in playing instead.
		follow.ReleaseEarly();
		Assert.True(follow.TryDecideSettle(generating: false, regenerating: false, liveWorld: true, out startsPlaying));
		Assert.True(startsPlaying);

		// A follow that is not following (or is already waiting) answers nothing.
		follow.WaitForReady();
		Assert.False(follow.TryDecideSettle(generating: false, regenerating: false, liveWorld: true, out _));
	}

	[Fact]
	public void TheHostsPresentationWindow_OpensOnItsOwnDescentAndClosesWithTheGate()
	{
		var follow = new HostDescentFollow();

		follow.BeginHostDescent();
		Assert.True(follow.HostPending);

		follow.CloseHostDescent();
		Assert.False(follow.HostPending);

		// Ending the follow closes the window too: a torn-down run must not leave it open.
		follow.BeginHostDescent();
		follow.End();
		Assert.False(follow.HostPending);
	}
}
