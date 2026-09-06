using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session.AdaptiveSync;

public class AdaptiveTrafficEstimatorTests
{
	private const ulong Peer = 2001;

	[Fact]
	public void KernelStream_ExtractsPerPeerSendRate()
	{
		var tracker = new NetworkTrafficTracker(1000);
		tracker.RecordSend(Peer, NetMsg.KernelEnvelope, 400, true, WirePayloadType.PlayerStateStream);
		tracker.RecordSend(Peer, NetMsg.KernelEnvelope, 600, true, WirePayloadType.PlayerStateStream);
		tracker.RecordSend(999, NetMsg.KernelEnvelope, 900, true, WirePayloadType.PlayerStateStream);

		var estimate = new AdaptiveTrafficEstimator().Estimate(
			tracker.Snapshot(),
			AdaptiveStreamId.PlayerStateBroadcast,
			Peer);

		Assert.True(estimate.HasObservation);
		Assert.Equal(2, estimate.SendCount);
		Assert.Equal(1000, estimate.SendBytes);
		Assert.Equal(1000, estimate.SendBytesPerSecond, 3);
		Assert.Equal(500, estimate.AverageSendBytes, 3);
		Assert.Equal(1000, estimate.PeerTotalSendBytesPerSecond, 3);
	}

	[Fact]
	public void TutorialClaw_ExtractsDirectMessageRate()
	{
		var tracker = new NetworkTrafficTracker(1000);
		tracker.RecordSend(Peer, NetMsg.TutorialClawState, 250, true);

		var estimate = new AdaptiveTrafficEstimator().Estimate(
			tracker.Snapshot(),
			AdaptiveStreamId.TutorialClawBroadcast,
			Peer);

		Assert.True(estimate.HasObservation);
		Assert.Equal(1, estimate.SendCount);
		Assert.Equal(250, estimate.SendBytesPerSecond, 3);
	}

	[Fact]
	public void MissingStreamForPeer_UsesPeerTotalsAsBandwidthEvidence()
	{
		var tracker = new NetworkTrafficTracker(1000);
		tracker.RecordSend(Peer, NetMsg.Ping, 10, true);
		tracker.RecordSend(Peer, NetMsg.KernelEnvelope, 500, true, WirePayloadType.EnemyStateStream);

		var estimate = new AdaptiveTrafficEstimator().Estimate(
			tracker.Snapshot(),
			AdaptiveStreamId.PlayerStateBroadcast,
			Peer);

		Assert.True(estimate.HasObservation, "peer totals are bandwidth evidence even when this stream has not sent yet.");
		Assert.Equal(0, estimate.SendCount);
		Assert.Equal(510, estimate.PeerTotalSendBytesPerSecond, 3);
	}
}
