using System;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Networking;

public class NetworkTrafficTrackerTests
{
	[Fact]
	public void RecordSendAndReceive_AccumulateTotalsPerMessageAndPeer()
	{
		var tracker = new NetworkTrafficTracker(1000);

		tracker.RecordSend(1, NetMsg.Ping, 10, true);
		tracker.RecordSend(1, NetMsg.Ping, 12, true);
		tracker.RecordSend(2, NetMsg.Ping, 8, false);
		tracker.RecordReceive(2, NetMsg.Pong, 11);

		var window = tracker.Snapshot();
		Assert.Equal(3, window.SendCount);
		Assert.Equal(30, window.SendBytes);
		Assert.Equal(1, window.ReceiveCount);
		Assert.Equal(11, window.ReceiveBytes);
		Assert.Equal(1, window.FailedSendCount);
		Assert.Equal(8, window.FailedSendBytes);

		var ping = window.SendByMessage[NetMsg.Ping];
		Assert.Equal(3, ping.Count);
		Assert.Equal(30, ping.Bytes);
		Assert.Equal(1, ping.FailedCount);
		Assert.Equal(8, ping.FailedBytes);

		var pong = window.ReceiveByMessage[NetMsg.Pong];
		Assert.Equal(1, pong.Count);
		Assert.Equal(11, pong.Bytes);

		var peer1 = window.ByPeer[1];
		Assert.Equal(2, peer1.SendCount);
		Assert.Equal(22, peer1.SendBytes);
		Assert.Equal(0, peer1.ReceiveCount);

		var peer2 = window.ByPeer[2];
		Assert.Equal(1, peer2.SendCount);
		Assert.Equal(8, peer2.SendBytes);
		Assert.Equal(1, peer2.ReceiveCount);
		Assert.Equal(11, peer2.ReceiveBytes);
		Assert.Equal(1, peer2.FailedSendCount);
		Assert.Equal(8, peer2.FailedSendBytes);
	}

	[Fact]
	public void TryCollectWindow_RollsAndResetsWithoutDoubleCounting()
	{
		var tracker = new NetworkTrafficTracker(1000);
		tracker.RecordSend(1, NetMsg.Ping, 10, true);

		Assert.False(tracker.TryCollectWindow(999, out _));
		Assert.True(tracker.TryCollectWindow(1000, out var window));
		Assert.Equal(1, window.SendCount);
		Assert.Equal(0, tracker.Snapshot().SendCount);
	}

	[Fact]
	public void Snapshot_DoesNotResetTheWindow()
	{
		var tracker = new NetworkTrafficTracker(1000);
		tracker.RecordSend(1, NetMsg.Ping, 10, true);

		Assert.Equal(1, tracker.Snapshot().SendCount);
		Assert.Equal(1, tracker.Snapshot().SendCount);
	}

	[Fact]
	public void Constructor_RejectsNonPositiveWindow() =>
		Assert.Throws<ArgumentOutOfRangeException>(() => new NetworkTrafficTracker(0));
}
