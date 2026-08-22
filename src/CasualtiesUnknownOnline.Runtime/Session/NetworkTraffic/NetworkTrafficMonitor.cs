using System;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;

/// <summary>
/// The whole-protocol traffic observer. It owns the rolling window and the
/// periodic log edge; <see cref="PacketSender"/> and <see cref="PacketReceiver"/>
/// only report raw send/receive facts to it. Observability-only — no batching,
/// no rate-limit, no bandwidth decision is made from these numbers yet.
/// </summary>
public sealed class NetworkTrafficMonitor(ITimeSource time, ILogger<NetworkTrafficMonitor> log) : ICuoService
{
	private readonly NetworkTrafficTracker _tracker = new(NetworkTrafficTracker.DefaultWindowMs);
	private readonly ITimeSource _time = time;
	private readonly ILogger<NetworkTrafficMonitor> _log = log;

	internal void RecordSend(ulong steamId, NetMsg msg, int byteCount, bool success) =>
		_tracker.RecordSend(steamId, msg, byteCount, success);

	internal void RecordReceive(ulong steamId, NetMsg msg, int byteCount) =>
		_tracker.RecordReceive(steamId, msg, byteCount);

	internal NetworkTrafficWindow CurrentWindow => _tracker.Snapshot();

	internal void Reset() => _tracker.Reset();

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update()
	{
		if (_tracker.TryCollectWindow(_time.NowMs, out var window) && window.TotalFrames > 0)
		{
			_log.LogInformation("[NetworkTraffic] {Window}", NetworkTrafficWindowLog.Format(window));
		}
	}

	void ICuoService.Stop()
	{
	}

	void IDisposable.Dispose()
	{
	}
}
