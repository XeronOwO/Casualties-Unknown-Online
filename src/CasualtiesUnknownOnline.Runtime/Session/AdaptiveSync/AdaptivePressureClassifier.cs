using System;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;

namespace CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;

/// <summary>
/// Pure classifier from peer-health and traffic evidence to a coarse pressure
/// level. Absent health means no evidence of pressure, so the stream stays at
/// its configured default; absent traffic likewise adds no pressure signal.
/// Stage 2 adds measured per-peer/per-stream bandwidth and failed-send evidence
/// to the Stage 1 RTT/jitter/loss inputs.
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

	internal const double ModeratePeerTotalBytesPerSecond = 128 * 1024;
	internal const double HighPeerTotalBytesPerSecond = 512 * 1024;
	internal const double CriticalPeerTotalBytesPerSecond = 2 * 1024 * 1024;

	internal const double ModerateStreamBytesPerSecond = 32 * 1024;
	internal const double HighStreamBytesPerSecond = 128 * 1024;
	internal const double CriticalStreamBytesPerSecond = 512 * 1024;

	internal const double ModerateFailedSendPercent = 2d;
	internal const double HighFailedSendPercent = 8d;
	internal const double CriticalFailedSendPercent = 20d;

	public static AdaptivePressureLevel Classify(PeerHealthTracker.PeerHealthSnapshot? health) =>
		Classify(new AdaptivePressureInput(health, null));

	public static AdaptivePressureLevel Classify(AdaptivePressureInput input)
	{
		var health = ClassifyHealth(input.Health);
		var traffic = ClassifyTraffic(input.Traffic);
		return (AdaptivePressureLevel)Math.Max((int)health, (int)traffic);
	}

	private static AdaptivePressureLevel ClassifyHealth(PeerHealthTracker.PeerHealthSnapshot? health)
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

	private static AdaptivePressureLevel ClassifyTraffic(AdaptiveTrafficEstimate? traffic)
	{
		if (traffic is null || !traffic.HasObservation)
		{
			return AdaptivePressureLevel.Optimal;
		}

		var peerTotalBps = Math.Max(traffic.PeerTotalSendBytesPerSecond, traffic.PeerTotalReceiveBytesPerSecond);
		if (peerTotalBps >= CriticalPeerTotalBytesPerSecond
			|| traffic.SendBytesPerSecond >= CriticalStreamBytesPerSecond
			|| traffic.SendFailedPercent >= CriticalFailedSendPercent)
		{
			return AdaptivePressureLevel.Critical;
		}

		if (peerTotalBps >= HighPeerTotalBytesPerSecond
			|| traffic.SendBytesPerSecond >= HighStreamBytesPerSecond
			|| traffic.SendFailedPercent >= HighFailedSendPercent)
		{
			return AdaptivePressureLevel.High;
		}

		if (peerTotalBps >= ModeratePeerTotalBytesPerSecond
			|| traffic.SendBytesPerSecond >= ModerateStreamBytesPerSecond
			|| traffic.SendFailedPercent >= ModerateFailedSendPercent)
		{
			return AdaptivePressureLevel.Moderate;
		}

		return AdaptivePressureLevel.Optimal;
	}
}
