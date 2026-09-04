using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Protocol.Wire;
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
		IReadOnlyDictionary<ulong, PeerTraffic> byPeer,
		IReadOnlyDictionary<WirePayloadType, PayloadTraffic> sendByPayloadType,
		IReadOnlyDictionary<WirePayloadType, PayloadTraffic> receiveByPayloadType)
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
		SendByPayloadType = sendByPayloadType;
		ReceiveByPayloadType = receiveByPayloadType;
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

	/// <summary>Semantic kernel-payload send frames grouped by <see cref="WirePayloadType"/>.</summary>
	internal IReadOnlyDictionary<WirePayloadType, PayloadTraffic> SendByPayloadType { get; }

	/// <summary>Semantic kernel-payload receive frames grouped by <see cref="WirePayloadType"/>.</summary>
	internal IReadOnlyDictionary<WirePayloadType, PayloadTraffic> ReceiveByPayloadType { get; }

	internal long TotalBytes => SendBytes + ReceiveBytes;

	internal long TotalFrames => SendCount + ReceiveCount;

	internal double ElapsedSeconds => Math.Max(1, EndMs - StartMs) / 1000.0;

	internal double SendBytesPerSecond => SendBytes / ElapsedSeconds;

	internal double ReceiveBytesPerSecond => ReceiveBytes / ElapsedSeconds;

	internal double TotalBytesPerSecond => TotalBytes / ElapsedSeconds;

	/// <summary>One message family's counts in one direction.</summary>
	internal sealed record MessageTraffic(int Count, long Bytes, int FailedCount, long FailedBytes);

	/// <summary>One kernel payload family's frame-size distribution in one direction.</summary>
	internal sealed record PayloadTraffic(int Count, long Bytes, int P50Bytes, int P95Bytes, int MinBytes, int MaxBytes, int FailedCount, long FailedBytes);

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
