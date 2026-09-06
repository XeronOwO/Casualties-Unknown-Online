using CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session.AdaptiveSync;

public class AdaptiveRatePolicyTests
{
	private readonly AdaptiveRatePolicy _policy = new();

	[Fact]
	public void LatestWins_Optimal_KeepsConfiguredBaseHz()
	{
		var profile = Get(AdaptiveStreamId.PlayerStateBroadcast);

		var result = _policy.GetEffectiveHz(profile, AdaptivePressureLevel.Optimal, 20);

		Assert.Equal(20, result);
	}

	[Theory]
	[InlineData(AdaptivePressureLevel.Moderate)]
	[InlineData(AdaptivePressureLevel.High)]
	[InlineData(AdaptivePressureLevel.Critical)]
	public void LatestWins_Pressure_LowersEffectiveHz(AdaptivePressureLevel pressure)
	{
		var profile = Get(AdaptiveStreamId.PlayerStateBroadcast);

		var result = _policy.GetEffectiveHz(profile, pressure, 20);

		Assert.True(result < 20, $"{pressure} must degrade a loss-tolerating stream below its base cadence.");
	}

	[Fact]
	public void LatestWins_HighPressure_DoesNotGoBelowMinHz()
	{
		var profile = Get(AdaptiveStreamId.PlayerStateBroadcast);
		profile = profile with { MaxHz = 60, MinHz = 5 };

		var result = _policy.GetEffectiveHz(profile, AdaptivePressureLevel.Critical, 1);

		Assert.Equal(5, result);
	}

	[Fact]
	public void ReliableControl_IsNeverThrottled()
	{
		var profile = new AdaptiveStreamProfile(
			AdaptiveStreamId.PlayerStateBroadcast,
			"ReliableTest",
			AdaptiveStreamDeliveryMode.ReliableControl,
			MinHz: 1,
			MaxHz: 60,
			Priority: 1);

		var result = _policy.GetEffectiveHz(profile, AdaptivePressureLevel.Critical, 20);

		Assert.Equal(20, result);
	}

	[Fact]
	public void Cumulative_IsNotAdaptedInStage1()
	{
		var profile = new AdaptiveStreamProfile(
			AdaptiveStreamId.PlayerStateBroadcast,
			"CumulativeTest",
			AdaptiveStreamDeliveryMode.Cumulative,
			MinHz: 1,
			MaxHz: 60,
			Priority: 1);

		var result = _policy.GetEffectiveHz(profile, AdaptivePressureLevel.Critical, 20);

		Assert.Equal(20, result);
	}

	[Theory]
	[InlineData(AdaptiveStreamDeliveryMode.ReliableControl)]
	[InlineData(AdaptiveStreamDeliveryMode.Cumulative)]
	public void NonLatestWins_IgnoresAdaptiveBounds(AdaptiveStreamDeliveryMode mode)
	{
		var profile = new AdaptiveStreamProfile(
			AdaptiveStreamId.PlayerStateBroadcast,
			"NonLatestWinsTest",
			mode,
			MinHz: 5,
			MaxHz: 10,
			Priority: 1);

		var result = _policy.GetEffectiveHz(profile, AdaptivePressureLevel.Critical, 1);

		Assert.Equal(1, result);
	}

	[Fact]
	public void HigherPriority_DegradesLessThanLowerPriority()
	{
		var high = Get(AdaptiveStreamId.PlayerStateBroadcast) with { Priority = 1 };
		var low = high with { Priority = 3 };

		var highResult = _policy.GetEffectiveHz(high, AdaptivePressureLevel.High, 20);
		var lowResult = _policy.GetEffectiveHz(low, AdaptivePressureLevel.High, 20);

		Assert.True(highResult >= lowResult, "high-priority streams must be preserved longer than low-priority streams.");
	}

	[Fact]
	public void LatestWins_ByteBudget_CapsEffectiveHz()
	{
		var profile = Get(AdaptiveStreamId.PlayerStateBroadcast) with
		{
			MaxBytesPerSecond = 1000,
			MinHz = 1,
			MaxHz = 60,
		};

		var result = _policy.GetEffectiveHz(profile, AdaptivePressureLevel.Optimal, 20, averageFrameBytes: 100);

		Assert.Equal(10, result);
	}

	[Fact]
	public void LatestWins_ByteBudget_DoesNotRaiseAbovePressureResult()
	{
		var profile = Get(AdaptiveStreamId.PlayerStateBroadcast) with
		{
			MaxBytesPerSecond = 100_000,
			MinHz = 1,
			MaxHz = 60,
		};

		var result = _policy.GetEffectiveHz(profile, AdaptivePressureLevel.Critical, 20, averageFrameBytes: 100);

		// Critical priority-1 factor is 0.25 -> 5 Hz; a generous byte budget does not raise it.
		Assert.Equal(5, result);
	}

	[Fact]
	public void LatestWins_ZeroByteBudget_DoesNotCap()
	{
		var profile = Get(AdaptiveStreamId.PlayerStateBroadcast) with
		{
			MaxBytesPerSecond = 0,
			MinHz = 1,
			MaxHz = 60,
		};

		var result = _policy.GetEffectiveHz(profile, AdaptivePressureLevel.Optimal, 20, averageFrameBytes: 100);

		Assert.Equal(20, result);
	}

	[Fact]
	public void LatestWins_ByteBudget_BelowMinHz_ClampsToMinHz()
	{
		var profile = Get(AdaptiveStreamId.PlayerStateBroadcast) with
		{
			MaxBytesPerSecond = 50,
			MinHz = 1,
			MaxHz = 60,
		};

		var result = _policy.GetEffectiveHz(profile, AdaptivePressureLevel.Critical, 20, averageFrameBytes: 100);

		Assert.Equal(1, result);
	}

	private static AdaptiveStreamProfile Get(AdaptiveStreamId id)
	{
		Assert.True(AdaptiveStreamCatalog.TryGet(id, out var profile), $"missing profile {id}");
		return profile;
	}
}
