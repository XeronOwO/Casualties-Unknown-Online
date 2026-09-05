using System;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The pure carry-participant pose rule behind the "carried character sits
/// after long idle" and "carrier can sit while carrying" regressions. While a
/// body is either half of a carry/piggyback relation, the native idle-sit must
/// be suppressed on every presentation path:
/// - a carry participant's own state publisher must not send Sitting=true;
/// - a carry-participant clone must not replay sit clips from the stream;
/// - an already-playing sit clip must be actively left;
/// - the idle timer must be held at zero so the sit condition cannot start.
/// Normal non-carried idle-sit behavior must remain unchanged.
/// </summary>
public class CarriedBodyPoseTests
{
	[Fact]
	public void NonCarriedIdleBody_MayPublishSitting() =>
		Assert.True(CarriedBodyPose.ShouldPublishSitting(
			isCarryParticipant: false,
			idleTimeExceeded: true,
			exercising: false));

	[Fact]
	public void CarriedRider_NeverPublishesSittingEvenWhenIdleTimerExceeded() =>
		Assert.False(CarriedBodyPose.ShouldPublishSitting(
			isCarryParticipant: true,
			idleTimeExceeded: true,
			exercising: false));

	[Fact]
	public void Carrier_NeverPublishesSittingEvenWhenIdleTimerExceeded() =>
		Assert.False(CarriedBodyPose.ShouldPublishSitting(
			isCarryParticipant: true,
			idleTimeExceeded: true,
			exercising: false));

	[Fact]
	public void ExercisingBody_DoesNotPublishSitting() =>
		Assert.False(CarriedBodyPose.ShouldPublishSitting(
			isCarryParticipant: false,
			idleTimeExceeded: true,
			exercising: true));

	[Fact]
	public void NonCarriedRemote_MayReplaySitFromStream() =>
		Assert.True(CarriedBodyPose.ShouldReplaySit(
			isCarryParticipant: false,
			entitySitting: true));

	[Fact]
	public void CarriedRiderClone_DoesNotReplaySitFromStream() =>
		Assert.False(CarriedBodyPose.ShouldReplaySit(
			isCarryParticipant: true,
			entitySitting: true));

	[Fact]
	public void CarrierClone_DoesNotReplaySitFromStream() =>
		Assert.False(CarriedBodyPose.ShouldReplaySit(
			isCarryParticipant: true,
			entitySitting: true));

	[Fact]
	public void CarriedRider_WithSitClip_ActivelyExitsSit() =>
		Assert.True(CarriedBodyPose.ShouldExitSit(
			isCarryParticipant: true,
			currentClipIsSit: true));

	[Fact]
	public void Carrier_WithSitClip_ActivelyExitsSit() =>
		Assert.True(CarriedBodyPose.ShouldExitSit(
			isCarryParticipant: true,
			currentClipIsSit: true));

	[Fact]
	public void NonCarriedBody_WithSitClip_IsNotForcedOutOfSit() =>
		Assert.False(CarriedBodyPose.ShouldExitSit(
			isCarryParticipant: false,
			currentClipIsSit: true));

	[Fact]
	public void CarriedRider_WithoutSitClip_NoExitNeeded() =>
		Assert.False(CarriedBodyPose.ShouldExitSit(
			isCarryParticipant: true,
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
		Assert.True(CarriedBodyPose.ShouldZeroIdleTimer(isCarryParticipant: true));

	[Fact]
	public void Carrier_HoldsIdleTimerAtZero() =>
		Assert.True(CarriedBodyPose.ShouldZeroIdleTimer(isCarryParticipant: true));

	[Fact]
	public void NonCarriedBody_DoesNotForceIdleTimerZero() =>
		Assert.False(CarriedBodyPose.ShouldZeroIdleTimer(isCarryParticipant: false));

	private static bool ShouldPublishBodyRoot(bool isCarried)
	{
		var method = typeof(CarriedBodyPose).GetMethod("ShouldPublishBodyRoot", BindingFlags.Static | BindingFlags.Public)
			?? throw new InvalidOperationException("CarriedBodyPose.ShouldPublishBodyRoot not found.");
		return (bool)method.Invoke(null, [isCarried])!;
	}

	[Fact]
	public void CarriedRide_PublishesBodyRootAsStreamAnchor() =>
		Assert.True(ShouldPublishBodyRoot(isCarried: true));

	[Fact]
	public void NonCarriedRagdollBody_KeepsTorsoAnchorConvention() =>
		Assert.False(ShouldPublishBodyRoot(isCarried: false));
}
