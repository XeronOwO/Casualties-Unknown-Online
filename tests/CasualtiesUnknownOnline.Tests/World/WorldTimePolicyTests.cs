using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The pure world-time policy: movement forces Normal and clears the request,
/// manual Fast/SuperFast requests are cooperative — they are ignored unless the
/// all-unconscious sleep branch already accelerates — all-unconscious sleep
/// acceleration applies only when every in-world ALIVE player is below the
/// game's black-screen threshold, a dying player slows the session to 3.5×,
/// dead players are ignored, and an unobserved player blocks every acceleration
/// (safety, never speed over a just-joined member).
/// </summary>
public class WorldTimePolicyTests
{
	private static WorldTimePlayerState Awake(float vx = 0f, float vy = 0f, float consciousness = 80f) =>
		new(true, true, consciousness, false, vx, vy);

	private static WorldTimePlayerState Asleep(bool brainDying = false, float consciousness = 10f) =>
		new(true, true, consciousness, brainDying, 0f, 0f);

	private static WorldTimePlayerState Dead() =>
		new(true, false, 0f, false, 0f, 0f);

	[Theory]
	[InlineData(WorldTimeSpeed.Fast)]
	[InlineData(WorldTimeSpeed.SuperFast)]
	public void AwakePlayer_BlocksManualAcceleration(WorldTimeSpeed requested)
	{
		var decision = WorldTimePolicy.Decide(requested, [Awake()]);

		Assert.Equal(WorldTimeSpeed.Normal, decision.Speed);
		Assert.Equal(WorldTimeSpeed.Normal, decision.NextRequested);
	}

	[Fact]
	public void NormalRequest_StandsForAwakeIdlePlayer()
	{
		var decision = WorldTimePolicy.Decide(WorldTimeSpeed.Normal, [Awake()]);

		Assert.Equal(WorldTimeSpeed.Normal, decision.Speed);
		Assert.Equal(WorldTimeSpeed.Normal, decision.NextRequested);
	}

	[Theory]
	[InlineData(0.6f, 0f)]
	[InlineData(0f, -0.6f)]
	[InlineData(0.4f, 0.4f)]
	public void MovingPlayer_ForcesNormalAndClearsRequest(float vx, float vy)
	{
		var decision = WorldTimePolicy.Decide(WorldTimeSpeed.SuperFast, [Awake(vx, vy)]);

		Assert.Equal(WorldTimeSpeed.Normal, decision.Speed);
		Assert.Equal(WorldTimeSpeed.Normal, decision.NextRequested);
	}

	[Fact]
	public void UnconsciousPlayerVelocity_DoesNotCountAsMovement()
	{
		// A ragdoll/unconscious body can carry velocity without input — sleep
		// acceleration must still be eligible.
		var decision = WorldTimePolicy.Decide(WorldTimeSpeed.Fast, [Asleep(consciousness: 10f) with { VelocityX = 2f }]);

		Assert.Equal(WorldTimeSpeed.UnconsciousFast, decision.Speed);
	}

	[Fact]
	public void AllUnconscious_AcceleratesTo25AndClearsRequest()
	{
		var decision = WorldTimePolicy.Decide(WorldTimeSpeed.Fast, [Asleep(), Asleep()]);

		Assert.Equal(WorldTimeSpeed.UnconsciousFast, decision.Speed);
		Assert.Equal(WorldTimeSpeed.Normal, decision.NextRequested);
	}

	[Fact]
	public void AnyDyingUnconscious_Uses35DyingFast()
	{
		var decision = WorldTimePolicy.Decide(WorldTimeSpeed.Normal, [Asleep(brainDying: true), Asleep()]);

		Assert.Equal(WorldTimeSpeed.DyingFast, decision.Speed);
	}

	[Fact]
	public void AnyAwakePlayer_BlocksManualAndSleepAcceleration()
	{
		var decision = WorldTimePolicy.Decide(WorldTimeSpeed.Fast, [Asleep(), Awake()]);

		Assert.Equal(WorldTimeSpeed.Normal, decision.Speed);
		Assert.Equal(WorldTimeSpeed.Normal, decision.NextRequested);
	}

	[Fact]
	public void DeadPlayers_AreIgnored()
	{
		var decision = WorldTimePolicy.Decide(WorldTimeSpeed.Normal, [Asleep(), Dead()]);

		Assert.Equal(WorldTimeSpeed.UnconsciousFast, decision.Speed);
	}

	[Fact]
	public void NoAlivePlayers_DoesNotAccelerate()
	{
		var decision = WorldTimePolicy.Decide(WorldTimeSpeed.Normal, [Dead(), Dead()]);

		Assert.Equal(WorldTimeSpeed.Normal, decision.Speed);
	}

	[Fact]
	public void UnknownPlayerState_ForcesNormalAndBlocksSleep()
	{
		var unknown = new WorldTimePlayerState(false, false, 0f, false, 0f, 0f);
		var decision = WorldTimePolicy.Decide(WorldTimeSpeed.Fast, [Asleep(), unknown]);

		Assert.Equal(WorldTimeSpeed.Normal, decision.Speed);
		Assert.Equal(WorldTimeSpeed.Normal, decision.NextRequested);
	}

	[Theory]
	[InlineData(WorldTimeSpeed.Normal, true)]
	[InlineData(WorldTimeSpeed.Fast, true)]
	[InlineData(WorldTimeSpeed.SuperFast, true)]
	[InlineData(WorldTimeSpeed.UnconsciousFast, false)]
	[InlineData(WorldTimeSpeed.DyingFast, false)]
	public void GuestRequests_OnlyManualSpeeds(WorldTimeSpeed speed, bool expected) =>
		Assert.Equal(expected, WorldTimePolicy.IsGuestRequestSpeed(speed));

	[Fact]
	public void EmptyWorld_DoesNotAccelerate()
	{
		var decision = WorldTimePolicy.Decide(WorldTimeSpeed.Fast, []);

		Assert.Equal(WorldTimeSpeed.Normal, decision.Speed);
		Assert.Equal(WorldTimeSpeed.Normal, decision.NextRequested);
	}
}
