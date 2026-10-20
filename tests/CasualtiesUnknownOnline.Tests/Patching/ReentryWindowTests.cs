using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The member's re-entry window is a pure state machine (no Unity, no session), so its every
/// transition is driven here through the adapter assembly. The case this class exists for is the
/// FIRST frame: the host's exit arrives while the member is still inside its old world (the menu
/// return is deferred to the frame-end seam), and an implementation that read "in a world" as
/// "back in the world" armed and dropped the window in that one frame, so the recovery never sent
/// a single ask in the scenario it was built for. The window now waits until it has observed the
/// member's own unload before a later "in world" counts as a return.
/// <para>
/// What the window does NOT own, and this class therefore does not claim: the ask itself (the
/// Runtime channel), the host's answer (<c>ReentryPolicy</c> + <c>SendWorldJoinTo</c>) and the
/// Unity reads that feed it. Those are covered by <c>ReentryRequestTests</c>,
/// <c>ReentryPolicyTests</c> and the acceptance batch.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class ReentryWindowTests
{
	private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

	private static readonly Type Window = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Run.ReentryWindow",
		throwOnError: true)!;

	private static object New() => Activator.CreateInstance(Window, nonPublic: true)!;

	private static bool TryArm(object window, long nowMs, bool memberInWorldOrEntering) =>
		(bool)Window.GetMethod("TryArm", Flags)!.Invoke(window, [nowMs, memberInWorldOrEntering])!;

	private static string Step(object window, long nowMs, bool inWorld, bool generating, bool entering, bool hostInWorld) =>
		Window.GetMethod("Step", Flags)!.Invoke(window, [nowMs, inWorld, generating, entering, hostInWorld])!.ToString()!;

	private static void NoteAsk(object window, long nowMs) =>
		Window.GetMethod("NoteAsk", Flags)!.Invoke(window, [nowMs]);

	private static bool Armed(object window) =>
		(bool)Window.GetProperty("Armed", Flags)!.GetValue(window)!;

	private static string CloseReason(object window) =>
		(string)Window.GetProperty("CloseReason", Flags)!.GetValue(window)!;

	private static long IntervalMs => (long)Window.GetField("IntervalMs", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

	private static int MaxRequests => (int)Window.GetField("MaxRequests", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

	private static long WindowMs => (long)Window.GetField("WindowMs", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

	/// <summary>
	/// The blocker's own case: the arming frame must not close the window. The member is still inside
	/// its old world there (the host pull is only recorded), and it becomes askable once its own world
	/// is gone and the host is back in one.
	/// </summary>
	[Fact]
	public void TheArmingFrame_DoesNotCloseTheWindow_TheMemberIsStillInItsOldWorld()
	{
		var window = New();
		Assert.True(TryArm(window, nowMs: 0, memberInWorldOrEntering: true));

		// Same frame, before the frame-end seam performs the deferred menu return: in the OLD world.
		Assert.Equal("Waiting", Step(window, nowMs: 0, inWorld: true, generating: false, entering: false, hostInWorld: false));
		Assert.True(Armed(window));

		// The world unloads; the host is not back yet.
		Assert.Equal("Waiting", Step(window, nowMs: 33, inWorld: false, generating: false, entering: false, hostInWorld: false));

		// The host is back in a world: the window may ask.
		Assert.Equal("Ready", Step(window, nowMs: 66, inWorld: false, generating: false, entering: false, hostInWorld: true));
	}

	[Fact]
	public void AMemberAlreadyInTheMenu_DoesNotArm()
	{
		var window = New();
		Assert.False(TryArm(window, nowMs: 0, memberInWorldOrEntering: false));
		Assert.False(Armed(window));
		Assert.Equal("Idle", Step(window, nowMs: 0, inWorld: false, generating: false, entering: false, hostInWorld: true));
	}

	[Fact]
	public void AMemberOnItsWayIn_DoesNotAsk()
	{
		var window = New();
		Assert.True(TryArm(window, nowMs: 0, memberInWorldOrEntering: true));

		// JoinPending / Starting / Generating: the instruction is in hand and the existing follow owns
		// the next step — the window waits rather than asking over it.
		Assert.Equal("Waiting", Step(window, nowMs: 33, inWorld: false, generating: true, entering: false, hostInWorld: true));
		Assert.Equal("Waiting", Step(window, nowMs: 66, inWorld: false, generating: false, entering: true, hostInWorld: true));
	}

	[Fact]
	public void AMemberBackInTheWorld_ClosesTheWindow_ButOnlyAfterItWasOut()
	{
		var window = New();
		Assert.True(TryArm(window, nowMs: 0, memberInWorldOrEntering: true));
		Assert.Equal("Waiting", Step(window, nowMs: 0, inWorld: true, generating: false, entering: false, hostInWorld: false));
		Assert.Equal("Ready", Step(window, nowMs: 33, inWorld: false, generating: false, entering: false, hostInWorld: true));
		NoteAsk(window, 33);

		Assert.Equal("Closed", Step(window, nowMs: 66, inWorld: true, generating: false, entering: false, hostInWorld: true));
		Assert.False(Armed(window));
		Assert.Contains("back in the world", CloseReason(window));
	}

	[Fact]
	public void TheAskCadence_IsFiveSeconds()
	{
		var window = New();
		Assert.True(TryArm(window, nowMs: 0, memberInWorldOrEntering: true));
		Assert.Equal("Ready", Step(window, nowMs: 33, inWorld: false, generating: false, entering: false, hostInWorld: true));
		NoteAsk(window, 33);
		Assert.Equal("Waiting", Step(window, nowMs: 34, inWorld: false, generating: false, entering: false, hostInWorld: true));
		Assert.Equal("Ready", Step(window, nowMs: 33 + IntervalMs, inWorld: false, generating: false, entering: false, hostInWorld: true));
	}

	[Fact]
	public void TheWindowSpendsABoundedNumberOfAsks_AndThenClosesByName()
	{
		var window = New();
		Assert.True(TryArm(window, nowMs: 0, memberInWorldOrEntering: true));
		var nowMs = 33L;
		for (var i = 0; i < MaxRequests; i++)
		{
			Assert.Equal("Ready", Step(window, nowMs, inWorld: false, generating: false, entering: false, hostInWorld: true));
			NoteAsk(window, nowMs);
			nowMs += IntervalMs;
		}

		Assert.Equal("Closed", Step(window, nowMs, inWorld: false, generating: false, entering: false, hostInWorld: true));
		Assert.Contains(MaxRequests.ToString(), CloseReason(window));
	}

	[Fact]
	public void AWindowThatNeverGetsAWorldEntry_ClosesAtItsDeadline()
	{
		var window = New();
		Assert.True(TryArm(window, nowMs: 0, memberInWorldOrEntering: true));
		Assert.Equal("Waiting", Step(window, nowMs: 33, inWorld: false, generating: false, entering: false, hostInWorld: false));

		Assert.Equal("Closed", Step(window, nowMs: WindowMs, inWorld: false, generating: false, entering: false, hostInWorld: false));
		Assert.Contains((WindowMs / 1000).ToString(), CloseReason(window));
	}

	[Fact]
	public void TheHostNotInAWorld_KeepsTheWindowWaitingInsteadOfAsking()
	{
		var window = New();
		Assert.True(TryArm(window, nowMs: 0, memberInWorldOrEntering: true));
		Assert.Equal("Waiting", Step(window, nowMs: 33, inWorld: false, generating: false, entering: false, hostInWorld: false));
	}

	[Fact]
	public void TheClockGoingBackwards_RestartsTheWindowInsteadOfStalling()
	{
		var window = New();
		Assert.True(TryArm(window, nowMs: 1_000, memberInWorldOrEntering: true));

		// Environment.TickCount wraps: the reading is now far behind the arm. The window restarts at
		// the new reading (Waiting) instead of treating the old arm as centuries in the future.
		Assert.Equal("Waiting", Step(window, nowMs: 10, inWorld: false, generating: false, entering: false, hostInWorld: true));
		Assert.Equal("Ready", Step(window, nowMs: 11, inWorld: false, generating: false, entering: false, hostInWorld: true));
	}
}
