using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;

namespace CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;

/// <summary>
/// Pure per-stream/per-peer bandwidth extractor. It reads the generic traffic
/// window already produced by <see cref="NetworkTrafficMonitor"/> and maps one
/// logical adaptive stream to its wire observation through
/// <see cref="AdaptiveStreamWireMapper"/>. There is no mutable state here:
/// the rate service decides which window (current or last completed) to feed.
/// </summary>
internal sealed class AdaptiveTrafficEstimator
{
	public AdaptiveTrafficEstimate Estimate(NetworkTrafficWindow window, AdaptiveStreamId streamId, ulong peerId)
	{
		if (!AdaptiveStreamWireMapper.TryGet(streamId, out var payloadType, out var message))
		{
			return Empty(peerId, window);
		}

		var sendCount = 0;
		var sendBytes = 0L;
		var sendFailedCount = 0;
		var sendFailedBytes = 0L;
		var receiveCount = 0;
		var receiveBytes = 0L;

		if (payloadType is { } type)
		{
			if (window.SendByPeerPayloadType.TryGetValue((peerId, type), out var send))
			{
				sendCount = send.Count;
				sendBytes = send.Bytes;
				sendFailedCount = send.FailedCount;
				sendFailedBytes = send.FailedBytes;
			}

			if (window.ReceiveByPeerPayloadType.TryGetValue((peerId, type), out var receive))
			{
				receiveCount = receive.Count;
				receiveBytes = receive.Bytes;
			}
		}
		else if (message is { } msg)
		{
			if (window.SendByPeerMessage.TryGetValue((peerId, msg), out var send))
			{
				sendCount = send.Count;
				sendBytes = send.Bytes;
				sendFailedCount = send.FailedCount;
				sendFailedBytes = send.FailedBytes;
			}

			if (window.ReceiveByPeerMessage.TryGetValue((peerId, msg), out var receive))
			{
				receiveCount = receive.Count;
				receiveBytes = receive.Bytes;
			}
		}

		var peerTotalSendBytesPerSecond = 0d;
		var peerTotalReceiveBytesPerSecond = 0d;
		if (window.ByPeer.TryGetValue(peerId, out var peer))
		{
			peerTotalSendBytesPerSecond = peer.SendBytes / window.ElapsedSeconds;
			peerTotalReceiveBytesPerSecond = peer.ReceiveBytes / window.ElapsedSeconds;
		}

		var hasObservation = sendCount > 0 || receiveCount > 0
			|| peerTotalSendBytesPerSecond > 0 || peerTotalReceiveBytesPerSecond > 0;
		return new AdaptiveTrafficEstimate(
			peerId,
			hasObservation,
			sendCount,
			sendBytes,
			sendBytes / window.ElapsedSeconds,
			sendFailedCount,
			sendFailedBytes,
			sendCount > 0 ? sendFailedCount * 100d / sendCount : 0d,
			receiveCount,
			receiveBytes,
			receiveBytes / window.ElapsedSeconds,
			peerTotalSendBytesPerSecond,
			peerTotalReceiveBytesPerSecond);
	}

	private static AdaptiveTrafficEstimate Empty(ulong peerId, NetworkTrafficWindow window)
	{
		var peerTotalSendBytesPerSecond = 0d;
		var peerTotalReceiveBytesPerSecond = 0d;
		if (window.ByPeer.TryGetValue(peerId, out var peer))
		{
			peerTotalSendBytesPerSecond = peer.SendBytes / window.ElapsedSeconds;
			peerTotalReceiveBytesPerSecond = peer.ReceiveBytes / window.ElapsedSeconds;
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
			peerTotalSendBytesPerSecond,
			peerTotalReceiveBytesPerSecond);
	}
}
