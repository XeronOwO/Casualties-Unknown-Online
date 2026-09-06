using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;

/// <summary>
/// The shared adaptive-rate query surface used by stream owners. It combines
/// the declarative stream catalog, the live per-peer health observation and
/// the pure pressure-to-rate policy into one answer: "what cadence should this
/// stream use right now?" Reliable control streams are not adapted by this
/// service; only loss-tolerant streams consult it for a rate.
/// </summary>
public sealed class AdaptiveStreamRateService : IDisposable
{
	private const double MinTrafficWindowElapsedSeconds = 1.0;

	private readonly NetworkTrafficMonitor _traffic;
	private readonly IOptionsMonitor<StateStreamOptions> _stateStreamOptions;
	private readonly ILogger<AdaptiveStreamRateService> _log;
	private readonly AdaptiveRatePolicy _policy = new();
	private readonly AdaptiveTrafficEstimator _trafficEstimator = new();
	private readonly Dictionary<(AdaptiveStreamId StreamId, ulong PeerId), int> _lastEffectiveHz = [];

	public AdaptiveStreamRateService(
		NetworkTrafficMonitor traffic,
		IOptionsMonitor<StateStreamOptions> stateStreamOptions,
		ILogger<AdaptiveStreamRateService> log)
	{
		_traffic = traffic;
		_stateStreamOptions = stateStreamOptions;
		_log = log;
		_traffic.ResetCompleted += Reset;
	}

	/// <summary>Effective Hz for a unicast stream to one peer.</summary>
	public int GetEffectiveHz(AdaptiveStreamId streamId, ulong peerId)
	{
		if (!AdaptiveStreamCatalog.TryGet(streamId, out var profile))
		{
			return _stateStreamOptions.CurrentValue.StateStreamHz;
		}

		var traffic = Estimate(streamId, peerId);
		return GetEffectiveHzForPeer(streamId, peerId, profile, traffic);
	}

	/// <summary>
	/// Effective Hz for a broadcast stream. Uses the most constrained peer so a
	/// single bad link cannot be overrun; if there are no peers, the default
	/// (optimal) cadence is returned.
	/// </summary>
	public int GetEffectiveHz(AdaptiveStreamId streamId, IEnumerable<ulong> peerIds)
	{
		if (!AdaptiveStreamCatalog.TryGet(streamId, out var profile))
		{
			return _stateStreamOptions.CurrentValue.StateStreamHz;
		}

		var any = false;
		var min = int.MaxValue;
		NetworkTrafficWindow? currentWindow = null;
		NetworkTrafficWindow? lastWindow = null;
		foreach (var peerId in peerIds)
		{
			if (!any)
			{
				currentWindow = _traffic.CurrentWindow;
				lastWindow = _traffic.LastCompletedWindow;
				any = true;
			}

			var traffic = Estimate(currentWindow!, lastWindow, streamId, peerId);
			min = Math.Min(min, GetEffectiveHzForPeer(streamId, peerId, profile, traffic));
		}

		if (any)
		{
			return min;
		}

		return _policy.GetEffectiveHz(
			profile,
			AdaptivePressureLevel.Optimal,
			_stateStreamOptions.CurrentValue.StateStreamHz);
	}

	/// <summary>Send interval for a unicast stream, in milliseconds.</summary>
	public long GetSendIntervalMs(AdaptiveStreamId streamId, ulong peerId) =>
		1000L / GetEffectiveHz(streamId, peerId);

	/// <summary>Send interval for a broadcast stream, in milliseconds.</summary>
	public long GetSendIntervalMs(AdaptiveStreamId streamId, IEnumerable<ulong> peerIds) =>
		1000L / GetEffectiveHz(streamId, peerIds);

	/// <summary>Clears the per-session rate-change cache when network health resets.</summary>
	public void Reset() => _lastEffectiveHz.Clear();

	public void Dispose() => _traffic.ResetCompleted -= Reset;

	private void LogRateChange(
		AdaptiveStreamId streamId,
		ulong peerId,
		string streamName,
		AdaptivePressureLevel pressure,
		int effectiveHz)
	{
		var key = (streamId, peerId);
		if (_lastEffectiveHz.TryGetValue(key, out var previous))
		{
			if (previous == effectiveHz)
			{
				return;
			}

			_log.LogDebug(
				"[AdaptiveSync] {Stream} peer {Peer} cadence {Previous}Hz -> {Effective}Hz (pressure {Pressure}).",
				streamName,
				peerId,
				previous,
				effectiveHz,
				pressure);
		}
		else
		{
			_log.LogDebug(
				"[AdaptiveSync] {Stream} peer {Peer} initial cadence {Effective}Hz (pressure {Pressure}).",
				streamName,
				peerId,
				effectiveHz,
				pressure);
		}

		_lastEffectiveHz[key] = effectiveHz;
	}

	private int GetEffectiveHzForPeer(
		AdaptiveStreamId streamId,
		ulong peerId,
		AdaptiveStreamProfile profile,
		AdaptiveTrafficEstimate traffic)
	{
		var pressure = Classify(peerId, traffic);
		var effective = _policy.GetEffectiveHz(
			profile,
			pressure,
			_stateStreamOptions.CurrentValue.StateStreamHz,
			traffic.AverageSendBytes);
		LogRateChange(streamId, peerId, profile.Name, pressure, effective);
		return effective;
	}

	private AdaptivePressureLevel Classify(ulong peerId, AdaptiveTrafficEstimate traffic)
	{
		var health = _traffic.TryGetHealthSnapshot(peerId, out var snapshot)
			? snapshot
			: null;
		return AdaptivePressureClassifier.Classify(new AdaptivePressureInput(health, traffic));
	}

	private AdaptiveTrafficEstimate Estimate(AdaptiveStreamId streamId, ulong peerId) =>
		Estimate(_traffic.CurrentWindow, _traffic.LastCompletedWindow, streamId, peerId);

	private AdaptiveTrafficEstimate Estimate(
		NetworkTrafficWindow currentWindow,
		NetworkTrafficWindow? lastWindow,
		AdaptiveStreamId streamId,
		ulong peerId)
	{
		if (currentWindow.ElapsedSeconds < MinTrafficWindowElapsedSeconds)
		{
			// A fresh/partial window is dominated by the initial join burst;
			// do not turn a few early frames into false bandwidth pressure.
			if (lastWindow is not null)
			{
				return _trafficEstimator.Estimate(lastWindow, streamId, peerId);
			}

			return new AdaptiveTrafficEstimate(
				peerId,
				false,
				0,
				0,
				0d,
				0,
				0,
				0d,
				0,
				0,
				0d,
				0d,
				0d);
		}

		var current = _trafficEstimator.Estimate(currentWindow, streamId, peerId);
		if (!current.HasObservation && lastWindow is not null)
		{
			return _trafficEstimator.Estimate(lastWindow, streamId, peerId);
		}

		return current;
	}
}
