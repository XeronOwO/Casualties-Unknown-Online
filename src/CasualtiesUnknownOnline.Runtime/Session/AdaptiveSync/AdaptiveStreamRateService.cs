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
	private readonly Dictionary<(AdaptiveStreamId StreamId, ulong PeerId), long> _lastEffectiveIntervalMs = [];

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
			GetBaseHz(profile));
	}

	/// <summary>Send interval for a unicast stream, in milliseconds.</summary>
	public long GetSendIntervalMs(AdaptiveStreamId streamId, ulong peerId) =>
		GetEffectiveIntervalMs(streamId, peerId);

	/// <summary>Send interval for a broadcast stream, in milliseconds.</summary>
	public long GetSendIntervalMs(AdaptiveStreamId streamId, IEnumerable<ulong> peerIds) =>
		GetEffectiveIntervalMs(streamId, peerIds);

	/// <summary>
	/// Effective send interval for a unicast stream in milliseconds. For Hz-based
	/// streams this delegates to the existing cadence query; for interval-based
	/// streams the policy applies the same pressure/priority factor to the
	/// profile's base interval.
	/// </summary>
	public long GetEffectiveIntervalMs(AdaptiveStreamId streamId, ulong peerId)
	{
		if (!AdaptiveStreamCatalog.TryGet(streamId, out var profile) || profile.BaseIntervalMs <= 0)
		{
			return 1000L / GetEffectiveHz(streamId, peerId);
		}

		var traffic = Estimate(streamId, peerId);
		return GetEffectiveIntervalMsForPeer(streamId, peerId, profile, traffic);
	}

	/// <summary>
	/// Effective send interval for a broadcast stream in milliseconds. Uses the
	/// most constrained peer (longest interval); if there are no peers the
	/// stream's optimal base interval is returned.
	/// </summary>
	public long GetEffectiveIntervalMs(AdaptiveStreamId streamId, IEnumerable<ulong> peerIds)
	{
		if (!AdaptiveStreamCatalog.TryGet(streamId, out var profile) || profile.BaseIntervalMs <= 0)
		{
			return 1000L / GetEffectiveHz(streamId, peerIds);
		}

		var any = false;
		long max = 0;
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
			max = Math.Max(max, GetEffectiveIntervalMsForPeer(streamId, peerId, profile, traffic));
		}

		if (any)
		{
			return max;
		}

		return _policy.GetEffectiveIntervalMs(
			profile,
			AdaptivePressureLevel.Optimal,
			profile.BaseIntervalMs);
	}

	/// <summary>Clears the per-session rate-change caches when network health resets.</summary>
	public void Reset()
	{
		_lastEffectiveHz.Clear();
		_lastEffectiveIntervalMs.Clear();
	}

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
			GetBaseHz(profile),
			traffic.AverageSendBytes);
		LogRateChange(streamId, peerId, profile.Name, pressure, effective);
		return effective;
	}

	private long GetEffectiveIntervalMsForPeer(
		AdaptiveStreamId streamId,
		ulong peerId,
		AdaptiveStreamProfile profile,
		AdaptiveTrafficEstimate traffic)
	{
		var pressure = Classify(peerId, traffic);
		var effective = _policy.GetEffectiveIntervalMs(
			profile,
			pressure,
			profile.BaseIntervalMs,
			traffic.AverageSendBytes);
		LogIntervalChange(streamId, peerId, profile.Name, pressure, effective);
		return effective;
	}

	private void LogIntervalChange(
		AdaptiveStreamId streamId,
		ulong peerId,
		string streamName,
		AdaptivePressureLevel pressure,
		long effectiveIntervalMs)
	{
		var key = (streamId, peerId);
		if (_lastEffectiveIntervalMs.TryGetValue(key, out var previous))
		{
			if (previous == effectiveIntervalMs)
			{
				return;
			}

			_log.LogDebug(
				"[AdaptiveSync] {Stream} peer {Peer} interval {Previous}ms -> {Effective}ms (pressure {Pressure}).",
				streamName,
				peerId,
				previous,
				effectiveIntervalMs,
				pressure);
		}
		else
		{
			_log.LogDebug(
				"[AdaptiveSync] {Stream} peer {Peer} initial interval {Effective}ms (pressure {Pressure}).",
				streamName,
				peerId,
				effectiveIntervalMs,
				pressure);
		}

		_lastEffectiveIntervalMs[key] = effectiveIntervalMs;
	}

	private int GetBaseHz(AdaptiveStreamProfile profile) =>
		profile.BaseHz > 0 ? profile.BaseHz : _stateStreamOptions.CurrentValue.StateStreamHz;

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
