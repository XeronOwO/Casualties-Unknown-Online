using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The pure world-time policy: the all-unconscious sleep acceleration owns the
/// clock while it applies (its speed, request cleared) and refuses to accelerate
/// over an unobserved player; otherwise the standing request is honored, so a
/// manual Fast/SuperFast request no longer needs an asleep group (user ruling
/// 2026-09-18). Movement is not an input at all: a movement key is the mover's
/// own action, taken on that player's client and reported like any other speed
/// change.
/// </summary>
public class WorldTimePolicyTests
{
	private static WorldTimePlayerState Awake(float consciousness = 80f) => new(true, true, consciousness, false);

	private static WorldTimePlayerState Asleep(bool brainDying = false, float consciousness = 10f) => new(true, true, consciousness, brainDying);

	private static WorldTimePlayerState Dead() => new(true, false, 0f, false);

	private static WorldTimePlayerState Unknown() => new(false, false, 0f, false);

	[Theory]
	[InlineData(WorldTimeSpeed.Fast)]
	[InlineData(WorldTimeSpeed.SuperFast)]
	public void ManualAcceleration_StandsWhileTeammatesAreAwake(WorldTimeSpeed requested)
	{
		var decision = WorldTimePolicy.Decide(requested, [Awake(), Awake(consciousness: 40f)]);

		Assert.Equal(requested, decision.Speed);
		Assert.Equal(requested, decision.NextRequested);
	}

	[Fact]
	public void ManualAcceleration_IsNotCancelledByAJustJoinedPlayer()
	{
		var decision = WorldTimePolicy.Decide(WorldTimeSpeed.Fast, [Awake(), Unknown()]);

		Assert.Equal(WorldTimeSpeed.Fast, decision.Speed);
		Assert.Equal(WorldTimeSpeed.Fast, decision.NextRequested);
	}

	[Fact]
	public void NormalRequest_StandsForAnAwakePlayer()
	{
		var decision = WorldTimePolicy.Decide(WorldTimeSpeed.Normal, [Awake()]);

		Assert.Equal(WorldTimeSpeed.Normal, decision.Speed);
		Assert.Equal(WorldTimeSpeed.Normal, decision.NextRequested);
	}

	[Fact]
	public void AllUnconscious_AcceleratesTo25AndClearsTheRequest()
	{
		var decision = WorldTimePolicy.Decide(WorldTimeSpeed.Fast, [Asleep(), Asleep()]);

		Assert.Equal(WorldTimeSpeed.UnconsciousFast, decision.Speed);
		Assert.Equal(WorldTimeSpeed.Normal, decision.NextRequested);
	}

	[Fact]
	public void SleepOwnsTheClockOverAStandingManualRequest()
	{
		var decision = WorldTimePolicy.Decide(WorldTimeSpeed.SuperFast, [Asleep(), Asleep()]);

		Assert.Equal(WorldTimeSpeed.UnconsciousFast, decision.Speed);
	}

	[Fact]
	public void AnyDyingUnconscious_Uses35DyingFast()
	{
		var decision = WorldTimePolicy.Decide(WorldTimeSpeed.Normal, [Asleep(brainDying: true), Asleep()]);

		Assert.Equal(WorldTimeSpeed.DyingFast, decision.Speed);
	}

	[Fact]
	public void AnyAwakePlayer_BlocksSleepAcceleration()
	{
		var decision = WorldTimePolicy.Decide(WorldTimeSpeed.Normal, [Asleep(), Awake()]);

		Assert.Equal(WorldTimeSpeed.Normal, decision.Speed);
		Assert.Equal(WorldTimeSpeed.Normal, decision.NextRequested);
	}

	[Fact]
	public void UnknownPlayerState_BlocksSleepAcceleration()
	{
		var decision = WorldTimePolicy.Decide(WorldTimeSpeed.Normal, [Asleep(), Unknown()]);

		Assert.Equal(WorldTimeSpeed.Normal, decision.Speed);
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
	public void AnUnrepresentableRequest_DegradesToNormal()
	{
		var decision = WorldTimePolicy.Decide((WorldTimeSpeed)7, [Awake()]);

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

	[Theory]
	[InlineData(WorldTimeSpeed.Normal, WorldTimeSpeed.Normal)]
	[InlineData(WorldTimeSpeed.Fast, WorldTimeSpeed.Fast)]
	[InlineData(WorldTimeSpeed.SuperFast, WorldTimeSpeed.SuperFast)]
	[InlineData(WorldTimeSpeed.UnconsciousFast, WorldTimeSpeed.UnconsciousFast)]
	[InlineData(WorldTimeSpeed.DyingFast, WorldTimeSpeed.DyingFast)]
	[InlineData((WorldTimeSpeed)9, WorldTimeSpeed.Normal)]
	public void NormalizeSpeed_KeepsTheFiveWireSpeedsAndRejectsTheRest(WorldTimeSpeed speed, WorldTimeSpeed expected) =>
		Assert.Equal(expected, WorldTimePolicy.NormalizeSpeed(speed));
}
