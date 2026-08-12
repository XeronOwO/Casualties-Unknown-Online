using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The position-domain motion thresholds (ItemMotionState): the shared
/// "settled" criterion — the host's stream throttle and the guest's follow
/// decision must agree on where "at rest" starts, or a settled item is
/// streamed at the wrong rate / eased when it should ride the stream.
/// </summary>
public class ItemMotionStateTests
{
	[Fact]
	public void Settled_AtRest_True() => Assert.True(ItemMotionState.IsSettled(0f, 0f));

	[Fact]
	public void Settled_BelowNoiseFloor_True() =>
		// Just under the thresholds — at rest.
		Assert.True(ItemMotionState.IsSettled(0.009f, 0.09f));

	[Fact]
	public void Settled_ExactThreshold_NotSettled()
	{
		// The comparisons are strict — the exact thresholds are still moving
		// (a velocity of exactly 0.1 is not "below the noise floor").
		Assert.False(ItemMotionState.IsSettled(0.01f, 0f), "velocity at the threshold is not below it");
		Assert.False(ItemMotionState.IsSettled(0f, 0.1f), "angular velocity at the threshold is not below it");
	}

	[Fact]
	public void Settled_OverThreshold_NotSettled()
	{
		Assert.False(ItemMotionState.IsSettled(0.02f, 0f), "velocity over the floor is moving");
		Assert.False(ItemMotionState.IsSettled(0f, 0.5f), "spin is moving");
		Assert.False(ItemMotionState.IsSettled(0.02f, 0.5f));
	}

	[Fact]
	public void Settled_FastVelocity_NotSettled() => Assert.False(ItemMotionState.IsSettled(100f, 0f));
}
