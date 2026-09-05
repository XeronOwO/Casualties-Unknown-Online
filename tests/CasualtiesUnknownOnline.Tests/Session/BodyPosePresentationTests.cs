using System;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The pure body-pose presentation rule behind the severe-sleepiness posture
/// desync. The owner's HandleVisuals feeds
/// <c>max(crouchAmount, 1 - legSpeedMult)</c> into the CrouchAmount animator
/// parameter; a render proxy previously used only its own crouchAmount, so a
/// severely sleepy (weak) owner still showed a straight standing clone.
/// </summary>
public class BodyPosePresentationTests
{
	[Fact]
	public void WeakLegSpeed_AddsSlouchInput()
	{
		var result = BodyPosePresentation.ProxyCrouchInput(
			crouchAmount: 0f,
			legSpeedMult: 0.3f);

		Assert.True(Math.Abs(result - 0.7f) < 0.001f, $"expected the leg-speed slouch, got {result}");
	}

	[Fact]
	public void FullLegSpeed_LeavesActualCrouchOnly()
	{
		var result = BodyPosePresentation.ProxyCrouchInput(
			crouchAmount: 0.2f,
			legSpeedMult: 1f);

		Assert.True(Math.Abs(result - 0.2f) < 0.001f, $"expected only the crouch amount, got {result}");
	}

	[Fact]
	public void CrouchingBody_RemainsFullyCrouched()
	{
		var result = BodyPosePresentation.ProxyCrouchInput(
			crouchAmount: 1f,
			legSpeedMult: 0.3f);

		Assert.True(Math.Abs(result - 1f) < 0.001f, $"expected full crouch, got {result}");
	}

	[Fact]
	public void OutOfRangeLegSpeed_IsClamped()
	{
		var low = BodyPosePresentation.ProxyCrouchInput(
			crouchAmount: 0f,
			legSpeedMult: -0.5f);
		var high = BodyPosePresentation.ProxyCrouchInput(
			crouchAmount: 0f,
			legSpeedMult: 1.5f);

		Assert.True(Math.Abs(low - 1f) < 0.001f, $"expected clamped low strength to slouch, got {low}");
		Assert.True(Math.Abs(high - 0f) < 0.001f, $"expected clamped high strength to stand, got {high}");
	}
}
