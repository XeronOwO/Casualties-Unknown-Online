using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;

/// <summary>
/// Immutable snapshot of one whole-protocol traffic observation window. Unlike
/// the item-domain tracker (which counts one logical operation once), this
/// window counts actual transport frames and their byte lengths — the number
/// that determines bandwidth. A send that the transport refused is still
/// counted as a failed send (not as delivered traffic).
/// </summary>
internal sealed class NetworkTrafficWindow
{
	internal NetworkTrafficWindow(
		long startMs,
		long endMs,
		int sendCount,
		long sendBytes,
		int receiveCount,
		long receiveBytes,
		int failedSendCount,
		long failedSendBytes,
		IReadOnlyDictionary<NetMsg, MessageTraffic> sendByMessage,
		IReadOnlyDictionary<NetMsg, MessageTraffic> receiveByMessage,
		IReadOnlyDictionary<ulong, PeerTraffic> byPeer)
	{
		StartMs = startMs;
		EndMs = endMs;
		SendCount = sendCount;
		SendBytes = sendBytes;
		ReceiveCount = receiveCount;
		ReceiveBytes = receiveBytes;
		FailedSendCount = failedSendCount;
		FailedSendBytes = failedSendBytes;
		SendByMessage = sendByMessage;
		ReceiveByMessage = receiveByMessage;
		ByPeer = byPeer;
	}

	internal long StartMs { get; }

	internal long EndMs { get; }

	internal int SendCount { get; }

	internal long SendBytes { get; }

	internal int ReceiveCount { get; }

	internal long ReceiveBytes { get; }

	internal int FailedSendCount { get; }

	internal long FailedSendBytes { get; }

	internal IReadOnlyDictionary<NetMsg, MessageTraffic> SendByMessage { get; }

	internal IReadOnlyDictionary<NetMsg, MessageTraffic> ReceiveByMessage { get; }

	internal IReadOnlyDictionary<ulong, PeerTraffic> ByPeer { get; }

	internal long TotalBytes => SendBytes + ReceiveBytes;

	internal long TotalFrames => SendCount + ReceiveCount;

	/// <summary>One message family's counts in one direction.</summary>
	internal sealed record MessageTraffic(int Count, long Bytes, int FailedCount, long FailedBytes);

	/// <summary>One peer's totals in one window.</summary>
	internal sealed record PeerTraffic(
		ulong SteamId,
		int SendCount,
		long SendBytes,
		int ReceiveCount,
		long ReceiveBytes,
		int FailedSendCount,
		long FailedSendBytes);
}
