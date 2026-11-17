using CasualtiesUnknownOnline.Runtime.Networking;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Networking;

public sealed class NetConditionsPolicyTests
{
	[Fact]
	public void Clamp_CapsLatencyJitterAndLoss()
	{
		var clamped = NetConditionsShim.Clamp(new NetConditionsProfile(true, 99_999, 99_999, 250));

		Assert.Equal(NetConditionsProfile.MaxLatencyMs, clamped.LatencyMs);
		Assert.Equal(NetConditionsProfile.MaxJitterMs, clamped.JitterMs);
		Assert.Equal(100, clamped.LossPercent);
		Assert.True(clamped.Enabled);
	}

	[Fact]
	public void Clamp_FloorsNegativeValues()
	{
		var clamped = NetConditionsShim.Clamp(new NetConditionsProfile(true, -10, -10, -10));

		Assert.Equal(0, clamped.LatencyMs);
		Assert.Equal(0, clamped.JitterMs);
		Assert.Equal(0, clamped.LossPercent);
	}

	[Fact]
	public void DropsUnreliable_NeverDropsAReliablePayload()
	{
		var profile = new NetConditionsProfile(true, 0, 0, 100);

		Assert.False(NetConditionsShim.DropsUnreliable(profile, reliable: true, roll: 0.0));
		Assert.False(NetConditionsShim.DropsUnreliable(profile, reliable: true, roll: 0.99));
	}

	[Fact]
	public void DropsUnreliable_DropsBelowThePercentageAndKeepsAtOrAbove()
	{
		var profile = new NetConditionsProfile(true, 0, 0, 20);

		Assert.True(NetConditionsShim.DropsUnreliable(profile, reliable: false, roll: 0.19));
		Assert.False(NetConditionsShim.DropsUnreliable(profile, reliable: false, roll: 0.20));
		Assert.False(NetConditionsShim.DropsUnreliable(new NetConditionsProfile(false, 0, 0, 100), reliable: false, roll: 0.0));
	}

	[Fact]
	public void DelayMs_AddsLatencyAndSpreadsJitterWithinItsBounds()
	{
		var profile = new NetConditionsProfile(true, 150, 50, 0);

		Assert.Equal(100, NetConditionsShim.DelayMs(profile, roll: 0.0));
		Assert.Equal(150, NetConditionsShim.DelayMs(profile, roll: 0.5));
		Assert.Equal(200, NetConditionsShim.DelayMs(profile, roll: 0.999));
		Assert.Equal(150, NetConditionsShim.DelayMs(new NetConditionsProfile(true, 150, 0, 0), roll: 0.25));
	}

	[Fact]
	public void DelayMs_IsZeroWhenTheProfileDoesNotShape()
	{
		Assert.Equal(0, NetConditionsShim.DelayMs(NetConditionsProfile.Off, roll: 0.5));
		Assert.Equal(0, NetConditionsShim.DelayMs(new NetConditionsProfile(true, 0, 0, 40), roll: 0.5));
	}
}
