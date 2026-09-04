using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The pure carried-ride pose rule behind the "carried character sits after
/// long idle" regression. While a body is being carried/piggybacked, the
/// native idle-sit must be suppressed on every presentation path:
/// - the rider's own state publisher must not send Sitting=true;
/// - a carrier-side rider clone must not replay sit clips from the stream;
/// - an already-playing sit clip must be actively left;
/// - the idle timer must be held at zero so the sit condition cannot start.
/// Normal non-carried idle-sit behavior must remain unchanged.
/// </summary>
public class CarriedBodyPoseTests
{
	[Fact]
	public void NonCarriedIdleBody_MayPublishSitting() =>
		Assert.True(CarriedBodyPose.ShouldPublishSitting(
			isCarried: false,
			idleTimeExceeded: true,
			exercising: false));

	[Fact]
	public void CarriedBody_NeverPublishesSittingEvenWhenIdleTimerExceeded() =>
		Assert.False(CarriedBodyPose.ShouldPublishSitting(
			isCarried: true,
			idleTimeExceeded: true,
			exercising: false));

	[Fact]
	public void ExercisingBody_DoesNotPublishSitting() =>
		Assert.False(CarriedBodyPose.ShouldPublishSitting(
			isCarried: false,
			idleTimeExceeded: true,
			exercising: true));

	[Fact]
	public void NonCarriedRemote_MayReplaySitFromStream() =>
		Assert.True(CarriedBodyPose.ShouldReplaySit(
			isCarriedRider: false,
			entitySitting: true));

	[Fact]
	public void CarriedRiderClone_DoesNotReplaySitFromStream() =>
		Assert.False(CarriedBodyPose.ShouldReplaySit(
			isCarriedRider: true,
			entitySitting: true));

	[Fact]
	public void CarriedRider_WithSitClip_ActivelyExitsSit() =>
		Assert.True(CarriedBodyPose.ShouldExitSit(
			isCarriedRider: true,
			currentClipIsSit: true));

	[Fact]
	public void NonCarriedBody_WithSitClip_IsNotForcedOutOfSit() =>
		Assert.False(CarriedBodyPose.ShouldExitSit(
			isCarriedRider: false,
			currentClipIsSit: true));

	[Fact]
	public void CarriedRider_WithoutSitClip_NoExitNeeded() =>
		Assert.False(CarriedBodyPose.ShouldExitSit(
			isCarriedRider: true,
			currentClipIsSit: false));

	[Fact]
	public void SittingEnd_RestoresGrounded()
	{
		Assert.True(CarriedBodyPose.ShouldRestoreGroundedOnSitEnd(
			entitySitting: false,
			previousSitting: true));
	}

	[Fact]
	public void SittingStart_DoesNotRestoreGrounded()
	{
		Assert.False(CarriedBodyPose.ShouldRestoreGroundedOnSitEnd(
			entitySitting: true,
			previousSitting: false));
	}

	[Fact]
	public void ContinuingSit_DoesNotRestoreGrounded()
	{
		Assert.False(CarriedBodyPose.ShouldRestoreGroundedOnSitEnd(
			entitySitting: true,
			previousSitting: true));
	}

	[Fact]
	public void AlreadyNotSitting_DoesNotRestoreGrounded()
	{
		Assert.False(CarriedBodyPose.ShouldRestoreGroundedOnSitEnd(
			entitySitting: false,
			previousSitting: false));
	}

	[Fact]
	public void CarriedRide_HoldsIdleTimerAtZero() =>
		Assert.True(CarriedBodyPose.ShouldZeroIdleTimer(isCarriedRide: true));

	[Fact]
	public void NonCarriedBody_DoesNotForceIdleTimerZero() =>
		Assert.False(CarriedBodyPose.ShouldZeroIdleTimer(isCarriedRide: false));
}
