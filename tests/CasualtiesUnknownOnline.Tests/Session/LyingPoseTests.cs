using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The pure lying-pose decision for the render proxy: the LayDown clips
/// approximate the owner's ragdoll when it is not standing OR not alive,
/// except while sleeping (the nap clips take over). This is the rule
/// SessionStatePump already used, extracted so the death/unconscious
/// presentation is L0-locked.
/// </summary>
public class LyingPoseTests
{
	[Fact]
	public void StandingAliveAwake_DoesNotLie() =>
		Assert.False(LyingPose.IsLying(standing: true, alive: true, sleeping: false));

	[Fact]
	public void NotStanding_Lies() =>
		// Unconsciousness/ragdoll collapses the body even while alive.
		Assert.True(LyingPose.IsLying(standing: false, alive: true, sleeping: false));

	[Fact]
	public void Dead_LiesEvenIfTheStandingFlagHasNotFallenYet() =>
		// Alive=false reaches the stream before the local Ragdoll flips
		// standing — the clone must lie immediately on the alive edge.
		Assert.True(LyingPose.IsLying(standing: true, alive: false, sleeping: false));

	[Fact]
	public void Sleeping_UsesTheNapClips_NotTheLyingPose()
	{
		Assert.False(LyingPose.IsLying(standing: false, alive: true, sleeping: true));
		Assert.False(LyingPose.IsLying(standing: true, alive: false, sleeping: true));
	}
}
