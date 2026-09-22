using System;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The initiator-side local clock state machine: a locally applied manual speed
/// suspends enforcement of the host's value until the host answers, an answer
/// equal to the intent confirms it, a different answer ramps the clock back to
/// the host's value (never a snap), and the ramp is driven by unscaled time so
/// the clock it moves cannot move it.
/// </summary>
public class WorldTimeLocalInitiationTests
{
	[Fact]
	public void BeginLocalInitiation_AheadOfTheHost_SuspendsEnforcement()
	{
		var initiation = new WorldTimeLocalInitiation();

		initiation.BeginLocalInitiation(WorldTimeSpeed.Fast);

		Assert.True(initiation.IsPending);
		Assert.Equal(WorldTimeSpeed.Fast, initiation.Pending);
		Assert.True(initiation.SuspendsEnforcement);
	}

	[Fact]
	public void BeginLocalInitiation_AtTheHostsOwnValue_NeedsNoReconciliation()
	{
		var initiation = new WorldTimeLocalInitiation();
		initiation.OnAuthoritative(WorldTimeSpeed.Fast, 5f);

		initiation.BeginLocalInitiation(WorldTimeSpeed.Fast);

		Assert.False(initiation.IsPending);
		Assert.False(initiation.SuspendsEnforcement);
	}

	[Fact]
	public void AnUnsynchronizedSpeed_IsIgnored()
	{
		var initiation = new WorldTimeLocalInitiation();

		initiation.BeginLocalInitiation((WorldTimeSpeed)42);

		Assert.False(initiation.IsPending);
		Assert.Equal(WorldTimeReconcile.None, initiation.OnAuthoritative((WorldTimeSpeed)42, 1f));
	}

	[Fact]
	public void AuthoritativeMatchingTheIntent_ConfirmsItWithoutWritingTheClock()
	{
		var initiation = new WorldTimeLocalInitiation();
		initiation.BeginLocalInitiation(WorldTimeSpeed.Fast);

		var reconcile = initiation.OnAuthoritative(WorldTimeSpeed.Fast, 5f);

		Assert.Equal(WorldTimeReconcile.None, reconcile);
		Assert.False(initiation.IsPending);
		Assert.False(initiation.SuspendsEnforcement);
		Assert.Equal(WorldTimeSpeed.Fast, initiation.Authoritative);
	}

	[Fact]
	public void AuthoritativeDifferingFromTheIntent_RampsBackInsteadOfSnapping()
	{
		var initiation = new WorldTimeLocalInitiation();
		initiation.BeginLocalInitiation(WorldTimeSpeed.Fast);

		var reconcile = initiation.OnAuthoritative(WorldTimeSpeed.Normal, 5f);

		Assert.Equal(WorldTimeReconcile.Ramp, reconcile);
		Assert.True(initiation.IsRamping);
		Assert.True(initiation.SuspendsEnforcement);
	}

	[Fact]
	public void Ramp_InterpolatesOverTheRampLength_AndEndsExactlyOnTheHostsValue()
	{
		var initiation = new WorldTimeLocalInitiation();
		initiation.BeginLocalInitiation(WorldTimeSpeed.Fast);
		initiation.OnAuthoritative(WorldTimeSpeed.Normal, 5f);

		var quarter = initiation.AdvanceRamp(WorldTimeLocalInitiation.RampSeconds * 0.25f);
		Assert.False(quarter.Done);
		AssertClose(4f, quarter.TimeScale);

		var half = initiation.AdvanceRamp(WorldTimeLocalInitiation.RampSeconds * 0.25f);
		Assert.False(half.Done);
		AssertClose(3f, half.TimeScale);

		var done = initiation.AdvanceRamp(WorldTimeLocalInitiation.RampSeconds * 0.5f);
		Assert.True(done.Done);
		AssertClose(1f, done.TimeScale);
		Assert.False(initiation.IsRamping);
		Assert.False(initiation.SuspendsEnforcement);
	}

	[Fact]
	public void Ramp_ToAHigherHostValue_AlsoInterpolates()
	{
		// The sleep gate can answer above the initiated speed (5× initiated, 25× owned).
		var initiation = new WorldTimeLocalInitiation();
		initiation.BeginLocalInitiation(WorldTimeSpeed.Fast);
		initiation.OnAuthoritative(WorldTimeSpeed.UnconsciousFast, 5f);

		var step = initiation.AdvanceRamp(WorldTimeLocalInitiation.RampSeconds * 0.5f);

		Assert.False(step.Done);
		AssertClose(15f, step.TimeScale);
	}

	[Fact]
	public void ATwoPressBurst_SettlesOnTheNewestIntent()
	{
		// The host answers in order, so the older answer can start a correction
		// ramp and the newest answer retargets it — the accepted transient of a
		// local initiation (the ticket's "rare rollback"), never a stuck clock.
		var initiation = new WorldTimeLocalInitiation();
		initiation.BeginLocalInitiation(WorldTimeSpeed.Fast);
		initiation.BeginLocalInitiation(WorldTimeSpeed.SuperFast);

		Assert.Equal(WorldTimeReconcile.Ramp, initiation.OnAuthoritative(WorldTimeSpeed.Fast, 20f));
		Assert.Equal(WorldTimeReconcile.Ramp, initiation.OnAuthoritative(WorldTimeSpeed.SuperFast, 12f));

		var done = initiation.AdvanceRamp(WorldTimeLocalInitiation.RampSeconds);
		Assert.True(done.Done);
		AssertClose(20f, done.TimeScale);
	}

	[Fact]
	public void AuthoritativeDuringARamp_WithTheSameTarget_KeepsTheRamp()
	{
		var initiation = new WorldTimeLocalInitiation();
		initiation.BeginLocalInitiation(WorldTimeSpeed.Fast);
		initiation.OnAuthoritative(WorldTimeSpeed.Normal, 5f);
		initiation.AdvanceRamp(WorldTimeLocalInitiation.RampSeconds * 0.5f);

		var again = initiation.OnAuthoritative(WorldTimeSpeed.Normal, 3f);

		Assert.Equal(WorldTimeReconcile.None, again);
		Assert.True(initiation.IsRamping);
		AssertClose(1f, initiation.AdvanceRamp(WorldTimeLocalInitiation.RampSeconds).TimeScale);
	}

	[Fact]
	public void AuthoritativeWhileIdle_IsAdoptedAtOnce()
	{
		var initiation = new WorldTimeLocalInitiation();

		var reconcile = initiation.OnAuthoritative(WorldTimeSpeed.SuperFast, 1f);

		Assert.Equal(WorldTimeReconcile.Adopt, reconcile);
		Assert.False(initiation.SuspendsEnforcement);
	}

	[Fact]
	public void RepeatedAuthoritative_IsIdempotent()
	{
		var initiation = new WorldTimeLocalInitiation();
		initiation.OnAuthoritative(WorldTimeSpeed.Fast, 1f);

		var reconcile = initiation.OnAuthoritative(WorldTimeSpeed.Fast, 5f);

		Assert.Equal(WorldTimeReconcile.None, reconcile);
	}

	[Fact]
	public void AdvanceRamp_WithoutARamp_DoesNothing()
	{
		var initiation = new WorldTimeLocalInitiation();

		var step = initiation.AdvanceRamp(1f);

		Assert.False(step.Done);
		AssertClose(0f, step.TimeScale);
	}

	[Fact]
	public void ResetSession_ClearsEverything()
	{
		var initiation = new WorldTimeLocalInitiation();
		initiation.BeginLocalInitiation(WorldTimeSpeed.Fast);
		initiation.OnAuthoritative(WorldTimeSpeed.Normal, 5f);

		initiation.ResetSessionState();

		Assert.Equal(WorldTimeSpeed.Normal, initiation.Authoritative);
		Assert.False(initiation.IsPending);
		Assert.False(initiation.IsRamping);
		Assert.False(initiation.SuspendsEnforcement);
		Assert.False(initiation.AdvanceRamp(1f).Done);
	}

	private static void AssertClose(float expected, float actual) =>
		Assert.True(Math.Abs(expected - actual) < 0.0001f, $"expected {expected} but was {actual}");
}
