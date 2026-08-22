using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Networking;

public class PeerHealthTrackerTests
{
	[Fact]
	public void RecordPingAndPong_UpdatesRttAverageAndJitter()
	{
		var tracker = new PeerHealthTracker();

		tracker.RecordPingSent(1, sendTicks: 1000, nowMs: 1000);
		tracker.RecordPong(1, rttMs: 50f, echoTicks: 1000);
		tracker.RecordPingSent(1, sendTicks: 6000, nowMs: 6000);
		tracker.RecordPong(1, rttMs: 80f, echoTicks: 6000);

		Assert.True(tracker.TryGetSnapshot(1, out var snapshot));
		Assert.Equal(80f, snapshot.LastRttMs);
		Assert.Equal(65f, snapshot.AverageRttMs);
		Assert.Equal(30f, snapshot.JitterMs);
		Assert.Equal(2, snapshot.PingsSent);
		Assert.Equal(2, snapshot.PingsCompleted);
		Assert.Equal(0, snapshot.PingsLost);
		Assert.Equal(0f, snapshot.LossPercent);
	}

	[Fact]
	public void UnansweredPing_IsCountedAsLossOnTheNextProbe()
	{
		var tracker = new PeerHealthTracker();

		tracker.RecordPingSent(1, sendTicks: 1000, nowMs: 1000);
		tracker.RecordPingSent(1, sendTicks: 6000, nowMs: 6000); // no pong came back for the first probe

		Assert.True(tracker.TryGetSnapshot(1, out var snapshot));
		Assert.Equal(2, snapshot.PingsSent);
		Assert.Equal(0, snapshot.PingsCompleted);
		Assert.Equal(1, snapshot.PingsLost);
		Assert.Equal(100f, snapshot.LossPercent);
	}

	[Fact]
	public void LatePongFromALostProbe_DoesNotCloseTheCurrentProbe()
	{
		var tracker = new PeerHealthTracker();

		tracker.RecordPingSent(1, sendTicks: 1000, nowMs: 1000);
		tracker.RecordPingSent(1, sendTicks: 6000, nowMs: 6000); // first probe lost, second is outstanding
		tracker.RecordPong(1, rttMs: 70f, echoTicks: 1000); // late old probe reply

		Assert.True(tracker.TryGetSnapshot(1, out var after));
		Assert.Equal(70f, after.LastRttMs);
		Assert.Equal(0, after.PingsCompleted);
		Assert.Equal(1, after.PingsLost);

		// The matching reply for the current outstanding probe still completes it.
		tracker.RecordPong(1, rttMs: 90f, echoTicks: 6000);
		Assert.True(tracker.TryGetSnapshot(1, out var completed));
		Assert.Equal(1, completed.PingsCompleted);
		Assert.Equal(1, completed.PingsLost);
	}

	[Fact]
	public void LateDuplicatePong_DoesNotCreateASecondSample()
	{
		var tracker = new PeerHealthTracker();

		tracker.RecordPingSent(1, sendTicks: 1000, nowMs: 1000);
		tracker.RecordPong(1, rttMs: 50f, echoTicks: 1000);
		tracker.RecordPong(1, rttMs: 50f, echoTicks: 1000); // duplicate transport delivery

		Assert.True(tracker.TryGetSnapshot(1, out var snapshot));
		Assert.Equal(1, snapshot.PingsCompleted);
		Assert.Equal(1, snapshot.PingsSent);
		Assert.Equal(50f, snapshot.AverageRttMs);
		Assert.Equal(0f, snapshot.JitterMs);
	}

	[Fact]
	public void Snapshots_AreOrderedAndResetClearsAllPeers()
	{
		var tracker = new PeerHealthTracker();
		tracker.RecordPingSent(2, sendTicks: 1000, nowMs: 1000);
		tracker.RecordPingSent(1, sendTicks: 1000, nowMs: 1000);

		var snapshots = tracker.Snapshots();
		Assert.Equal(2, snapshots.Count);
		Assert.Equal(1ul, snapshots[0].SteamId);
		Assert.Equal(2ul, snapshots[1].SteamId);

		tracker.Reset();
		Assert.Empty(tracker.Snapshots());
	}
}
