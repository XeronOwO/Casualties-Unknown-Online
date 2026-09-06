using CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session.AdaptiveSync;

public class AdaptivePressureTrafficClassifierTests
{
	[Fact]
	public void NoTrafficEvidence_IsOptimal()
	{
		var input = new AdaptivePressureInput(null, null);

		Assert.Equal(AdaptivePressureLevel.Optimal, AdaptivePressureClassifier.Classify(input));
	}

	[Theory]
	[InlineData(32 * 1024, AdaptivePressureLevel.Moderate)]
	[InlineData(128 * 1024, AdaptivePressureLevel.High)]
	[InlineData(512 * 1024, AdaptivePressureLevel.Critical)]
	public void PerStreamBandwidthThresholds_AreExact(double streamBps, AdaptivePressureLevel expected)
	{
		var input = new AdaptivePressureInput(null, Traffic(streamBytesPerSecond: streamBps));

		Assert.Equal(expected, AdaptivePressureClassifier.Classify(input));
	}

	[Theory]
	[InlineData(128 * 1024, AdaptivePressureLevel.Moderate)]
	[InlineData(512 * 1024, AdaptivePressureLevel.High)]
	[InlineData(2 * 1024 * 1024, AdaptivePressureLevel.Critical)]
	public void PeerTotalBandwidthThresholds_AreExact(double peerTotalBps, AdaptivePressureLevel expected)
	{
		var input = new AdaptivePressureInput(null, Traffic(peerTotalBytesPerSecond: peerTotalBps, streamBytesPerSecond: 100));

		Assert.Equal(expected, AdaptivePressureClassifier.Classify(input));
	}

	[Theory]
	[InlineData(2d, AdaptivePressureLevel.Moderate)]
	[InlineData(8d, AdaptivePressureLevel.High)]
	[InlineData(20d, AdaptivePressureLevel.Critical)]
	public void FailedSendThresholds_AreExact(double failedPercent, AdaptivePressureLevel expected)
	{
		var input = new AdaptivePressureInput(null, Traffic(failedSendPercent: failedPercent));

		Assert.Equal(expected, AdaptivePressureClassifier.Classify(input));
	}

	[Fact]
	public void TrafficPressure_TakesWorstWithHealthPressure()
	{
		var healthy = HealthySnapshot(rttMs: 20f);
		var critical = HealthySnapshot(rttMs: 500f);
		var healthCritical = new AdaptivePressureInput(critical, Traffic(streamBytesPerSecond: 100));
		var trafficModerate = new AdaptivePressureInput(healthy, Traffic(streamBytesPerSecond: 32 * 1024));

		Assert.Equal(AdaptivePressureLevel.Critical, AdaptivePressureClassifier.Classify(healthCritical));
		Assert.Equal(AdaptivePressureLevel.Moderate, AdaptivePressureClassifier.Classify(trafficModerate));
	}

	private static PeerHealthTracker.PeerHealthSnapshot HealthySnapshot(float rttMs) =>
		new(
			SteamId: 2001,
			LastRttMs: rttMs,
			AverageRttMs: rttMs,
			JitterMs: 5f,
			PingsSent: 10,
			PingsCompleted: 10,
			PingsLost: 0,
			LossPercent: 0f);

	private static AdaptiveTrafficEstimate Traffic(
		double streamBytesPerSecond = 0,
		double peerTotalBytesPerSecond = 0,
		double failedSendPercent = 0)
	{
		var hasObservation = streamBytesPerSecond > 0 || peerTotalBytesPerSecond > 0 || failedSendPercent > 0;
		return new AdaptiveTrafficEstimate(
			2001,
			hasObservation,
			hasObservation ? 1 : 0,
			hasObservation ? (long)streamBytesPerSecond : 0,
			streamBytesPerSecond,
			0,
			0,
			failedSendPercent,
			0,
			0,
			0,
			peerTotalBytesPerSecond,
			peerTotalBytesPerSecond);
	}
}
