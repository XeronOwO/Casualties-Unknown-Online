using CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session.AdaptiveSync;

public class AdaptivePressureClassifierTests
{
	[Fact]
	public void NoSnapshot_IsOptimal() =>
		Assert.Equal(AdaptivePressureLevel.Optimal, AdaptivePressureClassifier.Classify(null));

	[Fact]
	public void ModerateRtt_IsModerate()
	{
		var health = Snapshot(averageRttMs: 120f);

		Assert.Equal(AdaptivePressureLevel.Moderate, AdaptivePressureClassifier.Classify(health));
	}

	[Fact]
	public void HighRtt_IsHigh()
	{
		var health = Snapshot(averageRttMs: 250f);

		Assert.Equal(AdaptivePressureLevel.High, AdaptivePressureClassifier.Classify(health));
	}

	[Fact]
	public void CriticalRtt_IsCritical()
	{
		var health = Snapshot(averageRttMs: 500f);

		Assert.Equal(AdaptivePressureLevel.Critical, AdaptivePressureClassifier.Classify(health));
	}

	[Fact]
	public void CriticalLoss_IsCritical()
	{
		var health = Snapshot(lossPercent: 55f, pingsCompleted: 9, pingsLost: 11);

		Assert.Equal(AdaptivePressureLevel.Critical, AdaptivePressureClassifier.Classify(health));
	}

	[Fact]
	public void ModerateJitter_IsModerate()
	{
		var health = Snapshot(jitterMs: 40f);

		Assert.Equal(AdaptivePressureLevel.Moderate, AdaptivePressureClassifier.Classify(health));
	}

	[Theory]
	[InlineData(7.9f, AdaptivePressureLevel.Optimal)]
	[InlineData(8f, AdaptivePressureLevel.Moderate)]
	[InlineData(20f, AdaptivePressureLevel.High)]
	[InlineData(40f, AdaptivePressureLevel.Critical)]
	public void LossThresholds_AreExact(float lossPercent, AdaptivePressureLevel expected)
	{
		var health = Snapshot(lossPercent: lossPercent, pingsCompleted: 100, pingsLost: 0);

		Assert.Equal(expected, AdaptivePressureClassifier.Classify(health));
	}

	[Theory]
	[InlineData(99f, AdaptivePressureLevel.Optimal)]
	[InlineData(100f, AdaptivePressureLevel.Moderate)]
	[InlineData(200f, AdaptivePressureLevel.High)]
	[InlineData(400f, AdaptivePressureLevel.Critical)]
	public void RttThresholds_AreExact(float averageRttMs, AdaptivePressureLevel expected)
	{
		var health = Snapshot(averageRttMs: averageRttMs);

		Assert.Equal(expected, AdaptivePressureClassifier.Classify(health));
	}

	[Theory]
	[InlineData(29f, AdaptivePressureLevel.Optimal)]
	[InlineData(30f, AdaptivePressureLevel.Moderate)]
	[InlineData(60f, AdaptivePressureLevel.High)]
	[InlineData(100f, AdaptivePressureLevel.Critical)]
	public void JitterThresholds_AreExact(float jitterMs, AdaptivePressureLevel expected)
	{
		var health = Snapshot(jitterMs: jitterMs);

		Assert.Equal(expected, AdaptivePressureClassifier.Classify(health));
	}

	private static PeerHealthTracker.PeerHealthSnapshot Snapshot(
		float averageRttMs = 20f,
		float jitterMs = 5f,
		float lossPercent = 0f,
		int pingsCompleted = 10,
		int pingsLost = 0) =>
		new(
			SteamId: 2001,
			LastRttMs: averageRttMs,
			AverageRttMs: averageRttMs,
			JitterMs: jitterMs,
			PingsSent: pingsCompleted + pingsLost,
			PingsCompleted: pingsCompleted,
			PingsLost: pingsLost,
			LossPercent: lossPercent);
}
