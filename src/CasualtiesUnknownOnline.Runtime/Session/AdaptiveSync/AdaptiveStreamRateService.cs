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
	private readonly NetworkTrafficMonitor _traffic;
	private readonly IOptionsMonitor<StateStreamOptions> _stateStreamOptions;
	private readonly ILogger<AdaptiveStreamRateService> _log;
	private readonly AdaptiveRatePolicy _policy = new();
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

		var pressure = Classify(peerId);
		var effective = _policy.GetEffectiveHz(profile, pressure, _stateStreamOptions.CurrentValue.StateStreamHz);
		LogRateChange(streamId, peerId, profile.Name, pressure, effective);
		return effective;
	}

	/// <summary>
	/// Effective Hz for a broadcast stream. Uses the most constrained peer so a
	/// single bad link cannot be overrun; if there are no peers, the default
	/// (optimal) cadence is returned.
	/// </summary>
	public int GetEffectiveHz(AdaptiveStreamId streamId, IEnumerable<ulong> peerIds)
	{
		var any = false;
		var min = int.MaxValue;
		foreach (var peerId in peerIds)
		{
			any = true;
			min = Math.Min(min, GetEffectiveHz(streamId, peerId));
		}

		if (any)
		{
			return min;
		}

		if (!AdaptiveStreamCatalog.TryGet(streamId, out var profile))
		{
			return _stateStreamOptions.CurrentValue.StateStreamHz;
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

	private AdaptivePressureLevel Classify(ulong peerId) =>
		_traffic.TryGetHealthSnapshot(peerId, out var health)
			? AdaptivePressureClassifier.Classify(health)
			: AdaptivePressureLevel.Optimal;
}
