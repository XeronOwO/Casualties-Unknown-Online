using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;

/// <summary>
/// The whole-protocol traffic + peer-health observer. It owns the rolling
/// window and the periodic log edge; <see cref="PacketSender"/> and
/// <see cref="PacketReceiver"/> only report raw send/receive facts to it, and
/// <see cref="SessionService"/> reports the ping/pong probe results. It also
/// owns the pure <see cref="PeerHealthTracker"/> (RTT samples, jitter,
/// probe loss). The traffic counters remain observability-only; the peer
/// health snapshot is also consumed by <c>AdaptiveStreamRateService</c> for
/// the rate policy, while the rate decision itself lives in the adaptive-sync
/// layer rather than here.
/// </summary>
public sealed class NetworkTrafficMonitor(ITimeSource time, ILogger<NetworkTrafficMonitor> log) : ICuoService
{
	private readonly NetworkTrafficTracker _tracker = new(NetworkTrafficTracker.DefaultWindowMs);
	private readonly PeerHealthTracker _health = new();
	private readonly ITimeSource _time = time;
	private readonly ILogger<NetworkTrafficMonitor> _log = log;
	private NetworkTrafficWindow? _lastCompletedWindow;

	/// <summary>Raised after a per-session reset so dependent rate caches can clear.</summary>
	internal event Action? ResetCompleted;

	internal void RecordSend(ulong steamId, NetMsg msg, int byteCount, bool success, WirePayloadType? payloadType = null) =>
		_tracker.RecordSend(steamId, msg, byteCount, success, payloadType);

	internal void RecordReceive(ulong steamId, NetMsg msg, int byteCount) =>
		_tracker.RecordReceive(steamId, msg, byteCount);

	internal void RecordReceivePayload(ulong steamId, WirePayloadType payloadType, int byteCount) =>
		_tracker.RecordReceivePayload(steamId, payloadType, byteCount);

	internal void RecordPingSent(ulong steamId, long sendTicks, long nowMs) =>
		_health.RecordPingSent(steamId, sendTicks, nowMs);

	internal void RecordPong(ulong steamId, float rttMs, long echoTicks) =>
		_health.RecordPong(steamId, rttMs, echoTicks);

	internal NetworkTrafficWindow CurrentWindow => _tracker.Snapshot(_time.NowMs);

	/// <summary>The most recent window collected by <see cref="Update"/>; retained so rate queries still have a stable estimate immediately after a window roll.</summary>
	internal NetworkTrafficWindow? LastCompletedWindow => _lastCompletedWindow;

	internal IReadOnlyList<PeerHealthTracker.PeerHealthSnapshot> HealthSnapshots =>
		_health.Snapshots();

	internal bool TryGetHealthSnapshot(ulong steamId, out PeerHealthTracker.PeerHealthSnapshot snapshot) =>
		_health.TryGetSnapshot(steamId, out snapshot);

	internal void Reset()
	{
		_tracker.Reset(_time.NowMs);
		_health.Reset();
		_lastCompletedWindow = null;
		ResetCompleted?.Invoke();
	}

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update()
	{
		if (!_tracker.TryCollectWindow(_time.NowMs, out var window))
		{
			return;
		}

		_lastCompletedWindow = window;
		if (window.TotalFrames > 0)
		{
			_log.LogInformation("[NetworkTraffic] {Window}", NetworkTrafficWindowLog.Format(window));
		}

		var health = HealthSnapshots;
		if (health.Count > 0)
		{
			_log.LogInformation("[NetworkHealth] {Peers}", string.Join(", ", health.Select(FormatHealth)));
		}
	}

	void ICuoService.Stop()
	{
	}

	void IDisposable.Dispose()
	{
	}

	private static string FormatHealth(PeerHealthTracker.PeerHealthSnapshot snapshot)
	{
		var loss = snapshot.LossPercent;
		return $"peer={snapshot.SteamId} rtt={snapshot.LastRttMs:F1}ms avg={snapshot.AverageRttMs:F1}ms "
			+ $"jitter={snapshot.JitterMs:F1}ms loss={loss:F1}% ({snapshot.PingsCompleted}ok/{snapshot.PingsLost}lost)";
	}
}
