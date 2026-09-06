namespace CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;

/// <summary>
/// Per-peer, per-stream traffic/bandwidth estimate derived from one
/// <see cref="NetworkTraffic.NetworkTrafficWindow"/>. It carries the send side
/// (the side that owns the stream cadence), the matching receive side for
/// observability, and the peer's total send/receive rates so a stream can be
/// judged against both its own footprint and the peer link as a whole.
/// </summary>
internal sealed record AdaptiveTrafficEstimate(
	ulong PeerId,
	bool HasObservation,
	int SendCount,
	long SendBytes,
	double SendBytesPerSecond,
	int SendFailedCount,
	long SendFailedBytes,
	double SendFailedPercent,
	int ReceiveCount,
	long ReceiveBytes,
	double ReceiveBytesPerSecond,
	double PeerTotalSendBytesPerSecond,
	double PeerTotalReceiveBytesPerSecond)
{
	/// <summary>Average send frame size in bytes; 0 when nothing has been observed.</summary>
	public double AverageSendBytes => SendCount > 0 ? (double)SendBytes / SendCount : 0;
}
