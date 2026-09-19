using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The single speed ↔ Time.timeScale mapping the Game Adapter and the correction
/// ramp share (the multipliers mirror the game's own PlayerCamera.SetTimeScale
/// switch). Reading a live clock back refuses Paused, Slowmo and every value
/// between two speeds — a correction ramp is local presentation, not a domain
/// speed.
/// </summary>
public class WorldTimeSpeedScaleTests
{
	[Theory]
	[InlineData(WorldTimeSpeed.Normal, 1f)]
	[InlineData(WorldTimeSpeed.Fast, 5f)]
	[InlineData(WorldTimeSpeed.SuperFast, 20f)]
	[InlineData(WorldTimeSpeed.UnconsciousFast, 25f)]
	[InlineData(WorldTimeSpeed.DyingFast, 3.5f)]
	public void ToTimeScale_MirrorsTheGamesMultipliers(WorldTimeSpeed speed, float expected) =>
		Assert.Equal(expected, WorldTimeSpeedScale.ToTimeScale(speed));

	[Theory]
	[InlineData(1f, WorldTimeSpeed.Normal)]
	[InlineData(5f, WorldTimeSpeed.Fast)]
	[InlineData(20f, WorldTimeSpeed.SuperFast)]
	[InlineData(25f, WorldTimeSpeed.UnconsciousFast)]
	[InlineData(3.5f, WorldTimeSpeed.DyingFast)]
	public void FromTimeScale_ReadsADomainSpeedBack(float timeScale, WorldTimeSpeed expected) =>
		Assert.Equal(expected, WorldTimeSpeedScale.FromTimeScale(timeScale));

	[Theory]
	[InlineData(0f)] // Paused
	[InlineData(0.16f)] // Slowmo
	[InlineData(1.5f)]
	[InlineData(3f)] // mid-ramp
	[InlineData(12f)] // mid-ramp
	[InlineData(30f)]
	public void FromTimeScale_RejectsPresentationAndMidRampValues(float timeScale) =>
		Assert.Null(WorldTimeSpeedScale.FromTimeScale(timeScale));
}
