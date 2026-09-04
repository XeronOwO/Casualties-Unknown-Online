using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;

/// <summary>
/// Pure session-scoped whole-protocol traffic counter. It records one actual
/// transport frame per call (one per recipient, not one logical fan-out) and
/// rolls an immutable <see cref="NetworkTrafficWindow"/> every
/// <see cref="DefaultWindowMs"/>. The monitor owns the time edge; this class only
/// owns the counters and the window shape.
/// </summary>
internal sealed class NetworkTrafficTracker
{
	internal const long DefaultWindowMs = 10_000;

	private readonly long _windowMs;
	private readonly Dictionary<NetMsg, MessageAccumulator> _send = [];
	private readonly Dictionary<NetMsg, MessageAccumulator> _receive = [];
	private readonly Dictionary<ulong, PeerAccumulator> _peers = [];
	private readonly Dictionary<WirePayloadType, PayloadAccumulator> _sendByPayload = [];
	private readonly Dictionary<WirePayloadType, PayloadAccumulator> _receiveByPayload = [];
	private long _windowStartMs;
	private long _sendBytes;
	private long _receiveBytes;
	private long _failedSendBytes;
	private long _sendCount;
	private long _receiveCount;
	private long _failedSendCount;

	internal NetworkTrafficTracker(long windowMs)
	{
		if (windowMs <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(windowMs), "The traffic window must be positive.");
		}

		_windowMs = windowMs;
	}

	internal long WindowMs => _windowMs;

	internal long WindowStartMs => _windowStartMs;

	internal void RecordSend(ulong peer, NetMsg msg, int byteCount, bool success, WirePayloadType? payloadType = null)
	{
		_sendCount++;
		_sendBytes += byteCount;
		if (!success)
		{
			_failedSendCount++;
			_failedSendBytes += byteCount;
		}

		var message = GetOrCreate(_send, msg);
		message.Count++;
		message.Bytes += byteCount;
		if (!success)
		{
			message.FailedCount++;
			message.FailedBytes += byteCount;
		}

		if (payloadType is { } type)
		{
			var payload = GetOrCreatePayload(_sendByPayload, type);
			payload.Count++;
			payload.Bytes += byteCount;
			payload.RecordSize(byteCount);
			if (!success)
			{
				payload.FailedCount++;
				payload.FailedBytes += byteCount;
			}
		}

		var peerCounter = GetPeer(peer);
		peerCounter.SendCount++;
		peerCounter.SendBytes += byteCount;
		if (!success)
		{
			peerCounter.FailedSendCount++;
			peerCounter.FailedSendBytes += byteCount;
		}
	}

	internal void RecordReceive(ulong peer, NetMsg msg, int byteCount)
	{
		_receiveCount++;
		_receiveBytes += byteCount;

		var message = GetOrCreate(_receive, msg);
		message.Count++;
		message.Bytes += byteCount;

		var peerCounter = GetPeer(peer);
		peerCounter.ReceiveCount++;
		peerCounter.ReceiveBytes += byteCount;
	}

	internal void RecordReceivePayload(ulong peer, WirePayloadType payloadType, int byteCount)
	{
		var payload = GetOrCreatePayload(_receiveByPayload, payloadType);
		payload.Count++;
		payload.Bytes += byteCount;
		payload.RecordSize(byteCount);
	}

	internal bool TryCollectWindow(long nowMs, out NetworkTrafficWindow window)
	{
		if (nowMs - _windowStartMs < _windowMs)
		{
			window = null!;
			return false;
		}

		window = Build(_windowStartMs, nowMs);
		ResetTo(nowMs);
		return true;
	}

	internal NetworkTrafficWindow Snapshot() => Build(_windowStartMs, _windowStartMs + _windowMs);

	internal void Reset() => ResetTo(_windowStartMs);

	private NetworkTrafficWindow Build(long startMs, long endMs)
	{
		var sendByMessage = _send
			.Where(kv => kv.Value.Count > 0)
			.ToDictionary(
				kv => kv.Key,
				kv => new NetworkTrafficWindow.MessageTraffic(kv.Value.Count, kv.Value.Bytes, kv.Value.FailedCount, kv.Value.FailedBytes));
		var receiveByMessage = _receive
			.Where(kv => kv.Value.Count > 0)
			.ToDictionary(
				kv => kv.Key,
				kv => new NetworkTrafficWindow.MessageTraffic(kv.Value.Count, kv.Value.Bytes, 0, 0));
		var byPeer = _peers
			.Where(kv => kv.Value.SendCount > 0 || kv.Value.ReceiveCount > 0)
			.ToDictionary(
				kv => kv.Key,
				kv => new NetworkTrafficWindow.PeerTraffic(
					kv.Key,
					kv.Value.SendCount,
					kv.Value.SendBytes,
					kv.Value.ReceiveCount,
					kv.Value.ReceiveBytes,
					kv.Value.FailedSendCount,
					kv.Value.FailedSendBytes));

		var sendByPayload = _sendByPayload
			.Where(kv => kv.Value.Count > 0)
			.ToDictionary(kv => kv.Key, kv => kv.Value.ToPayloadTraffic());
		var receiveByPayload = _receiveByPayload
			.Where(kv => kv.Value.Count > 0)
			.ToDictionary(kv => kv.Key, kv => kv.Value.ToPayloadTraffic());

		return new NetworkTrafficWindow(
			startMs,
			endMs,
			(int)_sendCount,
			_sendBytes,
			(int)_receiveCount,
			_receiveBytes,
			(int)_failedSendCount,
			_failedSendBytes,
			sendByMessage,
			receiveByMessage,
			byPeer,
			sendByPayload,
			receiveByPayload);
	}

	private void ResetTo(long startMs)
	{
		_send.Clear();
		_receive.Clear();
		_peers.Clear();
		_sendByPayload.Clear();
		_receiveByPayload.Clear();
		_sendBytes = 0;
		_receiveBytes = 0;
		_failedSendBytes = 0;
		_sendCount = 0;
		_receiveCount = 0;
		_failedSendCount = 0;
		_windowStartMs = startMs;
	}

	private static MessageAccumulator GetOrCreate(Dictionary<NetMsg, MessageAccumulator> map, NetMsg msg)
	{
		if (!map.TryGetValue(msg, out var accumulator))
		{
			accumulator = new MessageAccumulator();
			map[msg] = accumulator;
		}

		return accumulator;
	}

	private PeerAccumulator GetPeer(ulong peer)
	{
		if (!_peers.TryGetValue(peer, out var accumulator))
		{
			accumulator = new PeerAccumulator();
			_peers[peer] = accumulator;
		}

		return accumulator;
	}

	private static PayloadAccumulator GetOrCreatePayload(Dictionary<WirePayloadType, PayloadAccumulator> map, WirePayloadType payloadType)
	{
		if (!map.TryGetValue(payloadType, out var accumulator))
		{
			accumulator = new PayloadAccumulator();
			map[payloadType] = accumulator;
		}

		return accumulator;
	}

	private sealed class MessageAccumulator
	{
		public int Count;
		public long Bytes;
		public int FailedCount;
		public long FailedBytes;
	}

	private sealed class PayloadAccumulator
	{
		private readonly Dictionary<int, int> _sizes = [];

		public int Count;
		public long Bytes;
		public int FailedCount;
		public long FailedBytes;

		public void RecordSize(int byteCount) =>
			_sizes[byteCount] = _sizes.TryGetValue(byteCount, out var count) ? count + 1 : 1;

		public NetworkTrafficWindow.PayloadTraffic ToPayloadTraffic()
		{
			var sorted = _sizes
				.OrderBy(kv => kv.Key)
				.ToList();
			var min = sorted[0].Key;
			var max = sorted[sorted.Count - 1].Key;
			return new NetworkTrafficWindow.PayloadTraffic(
				Count,
				Bytes,
				Percentile(sorted, 0.50),
				Percentile(sorted, 0.95),
				min,
				max,
				FailedCount,
				FailedBytes);
		}

		private int Percentile(List<KeyValuePair<int, int>> sorted, double percentile)
		{
			var rank = Math.Max(1, (int)Math.Ceiling(Count * percentile));
			var seen = 0;
			foreach (var pair in sorted)
			{
				seen += pair.Value;
				if (seen >= rank)
				{
					return pair.Key;
				}
			}

			return sorted[sorted.Count - 1].Key;
		}
	}

	private sealed class PeerAccumulator
	{
		public int SendCount;
		public long SendBytes;
		public int ReceiveCount;
		public long ReceiveBytes;
		public int FailedSendCount;
		public long FailedSendBytes;
	}
}
