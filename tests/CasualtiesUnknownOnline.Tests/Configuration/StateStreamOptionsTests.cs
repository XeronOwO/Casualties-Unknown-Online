using System;
using CasualtiesUnknownOnline.Runtime.Configuration;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Configuration;

/// <summary>
/// The state-stream cadence option is the single normalized source for every
/// formerly hard-coded 20 Hz stream: values outside 1-60 clamp, and the send
/// interval follows the clamped Hz (not the raw config value).
/// </summary>
public class StateStreamOptionsTests
{
	[Fact]
	public void Default_Is20Hz_50msInterval()
	{
		var options = new StateStreamOptions();

		Assert.Equal(20, options.StateStreamHz);
		Assert.True(Math.Abs(options.SendIntervalSeconds - 0.05f) < 0.001f,
			$"the default 20 Hz interval must be 50 ms, was {options.SendIntervalSeconds} s");
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(-5, 1)]
	[InlineData(1, 1)]
	[InlineData(5, 5)]
	[InlineData(60, 60)]
	[InlineData(120, 60)]
	public void StateStreamHz_ClampsIntoTheSupportedBand(int raw, int expected)
	{
		var options = new StateStreamOptions { StateStreamHz = raw };

		Assert.Equal(expected, options.StateStreamHz);
	}

	[Fact]
	public void SendInterval_FollowsTheClampedFrequency()
	{
		var options = new StateStreamOptions { StateStreamHz = 5 };

		Assert.True(Math.Abs(options.SendIntervalSeconds - 0.2f) < 0.001f,
			$"the 5 Hz interval must be 200 ms, was {options.SendIntervalSeconds} s");
	}
}
