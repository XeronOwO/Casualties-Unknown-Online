using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;

namespace CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;

/// <summary>
/// Pure classifier from the existing peer-health snapshot to a coarse pressure
/// level. An absent health snapshot means no evidence of pressure, so the
/// stream stays at its configured default.
/// </summary>
internal static class AdaptivePressureClassifier
{
	internal const float ModerateLossPercent = 8f;
	internal const float HighLossPercent = 20f;
	internal const float CriticalLossPercent = 40f;
	internal const float ModerateRttMs = 100f;
	internal const float HighRttMs = 200f;
	internal const float CriticalRttMs = 400f;
	internal const float ModerateJitterMs = 30f;
	internal const float HighJitterMs = 60f;
	internal const float CriticalJitterMs = 100f;

	public static AdaptivePressureLevel Classify(PeerHealthTracker.PeerHealthSnapshot? health)
	{
		if (health is null)
		{
			return AdaptivePressureLevel.Optimal;
		}

		var loss = health.LossPercent;
		var rtt = health.AverageRttMs;
		var jitter = health.JitterMs;

		if (loss >= CriticalLossPercent || rtt >= CriticalRttMs || jitter >= CriticalJitterMs)
		{
			return AdaptivePressureLevel.Critical;
		}

		if (loss >= HighLossPercent || rtt >= HighRttMs || jitter >= HighJitterMs)
		{
			return AdaptivePressureLevel.High;
		}

		if (loss >= ModerateLossPercent || rtt >= ModerateRttMs || jitter >= ModerateJitterMs)
		{
			return AdaptivePressureLevel.Moderate;
		}

		return AdaptivePressureLevel.Optimal;
	}
}
