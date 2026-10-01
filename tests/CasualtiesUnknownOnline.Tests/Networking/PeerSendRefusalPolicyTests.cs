using CasualtiesUnknownOnline.Runtime.Networking;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Networking;

/// <summary>
/// The pure send-refusal policy: gating, exponential backoff, aggregate report
/// windows, recovery and the stall escalation — every bound that keeps a refused
/// send from being retried per frame or logged per attempt.
/// </summary>
public class PeerSendRefusalPolicyTests
{
	private const ulong Peer = 7001;
	private const ulong OtherPeer = 7002;

	[Fact]
	public void QueueFull_GatesThePeer_AndDoublesTheDelayUpToTheCap()
	{
		var policy = new PeerSendRefusalPolicy();

		Assert.True(policy.RecordFailure(Peer, SteamSendFailureKind.QueueFull, 0)); // the episode's diagnostic line

		Assert.False(policy.TryBeginSend(Peer, 0));   // 250 ms backoff after the first refusal
		Assert.False(policy.TryBeginSend(Peer, 249));
		Assert.True(policy.TryBeginSend(Peer, 250));

		Assert.False(policy.RecordFailure(Peer, SteamSendFailureKind.QueueFull, 250)); // 250 -> 500
		Assert.False(policy.TryBeginSend(Peer, 749));
		Assert.True(policy.TryBeginSend(Peer, 750));

		Assert.False(policy.RecordFailure(Peer, SteamSendFailureKind.QueueFull, 750)); // 500 -> 1000
		Assert.False(policy.TryBeginSend(Peer, 1749));
		Assert.True(policy.TryBeginSend(Peer, 1750));

		Assert.False(policy.RecordFailure(Peer, SteamSendFailureKind.QueueFull, 1750)); // 1000 -> 2000
		Assert.False(policy.TryBeginSend(Peer, 3749));
		Assert.True(policy.TryBeginSend(Peer, 3750));

		Assert.False(policy.RecordFailure(Peer, SteamSendFailureKind.QueueFull, 3750)); // capped at 2000
		Assert.False(policy.TryBeginSend(Peer, 5749));
		Assert.True(policy.TryBeginSend(Peer, 5750));
	}

	[Fact]
	public void OtherFailureKinds_AreNotGated_ButStillAggregate()
	{
		var policy = new PeerSendRefusalPolicy();

		Assert.True(policy.RecordFailure(Peer, SteamSendFailureKind.NoConnection, 0)); // first line of the window
		Assert.False(policy.RecordFailure(Peer, SteamSendFailureKind.NoConnection, 1));
		Assert.False(policy.RecordFailure(Peer, SteamSendFailureKind.NoConnection, 2));

		// A dead session is re-driven by sending (AutoRestartBrokenSession), so the
		// gate must not hold the attempts back.
		Assert.True(policy.TryBeginSend(Peer, 3));

		var report = Assert.Single(policy.CollectDueReports(5_000));
		Assert.Equal(3, report.RefusedAttempts);
		Assert.Equal(0, report.SuppressedAttempts);
		Assert.Equal(0, report.CongestedForMs);

		// One report per window, and nothing new to report after it.
		Assert.Empty(policy.CollectDueReports(5_001));
	}

	[Fact]
	public void SuppressedAttempts_AreCountedIntoTheAggregate()
	{
		var policy = new PeerSendRefusalPolicy();

		policy.RecordFailure(Peer, SteamSendFailureKind.QueueFull, 0);
		Assert.False(policy.TryBeginSend(Peer, 1));
		Assert.False(policy.TryBeginSend(Peer, 2));
		Assert.False(policy.TryBeginSend(Peer, 3));

		var report = Assert.Single(policy.CollectDueReports(5_000));
		Assert.Equal(1, report.RefusedAttempts);
		Assert.Equal(3, report.SuppressedAttempts);
		Assert.Equal(5_000L, report.CongestedForMs);
	}

	[Fact]
	public void Success_ClearsTheEpisode_AndReportsItOnce()
	{
		var policy = new PeerSendRefusalPolicy();

		policy.RecordFailure(Peer, SteamSendFailureKind.QueueFull, 0);
		Assert.False(policy.TryBeginSend(Peer, 100));
		Assert.False(policy.TryBeginSend(Peer, 200));

		var recovery = policy.RecordSuccess(Peer, 400);
		Assert.True(recovery.HasValue, "the episode must report its recovery");
		Assert.Equal(400L, recovery.GetValueOrDefault().CongestedForMs);
		Assert.Equal(1, recovery.GetValueOrDefault().RefusedAttempts);
		Assert.Equal(2, recovery.GetValueOrDefault().SuppressedAttempts);

		// Not congested any more: no gate, no second recovery report.
		Assert.True(policy.TryBeginSend(Peer, 401));
		Assert.Null(policy.RecordSuccess(Peer, 402));
		Assert.Empty(policy.CollectDueReports(10_000));
	}

	[Fact]
	public void StallEscalation_FiresOncePerEpisode_WhileRefusalsContinue()
	{
		var policy = new PeerSendRefusalPolicy();

		for (var nowMs = 0L; nowMs <= 31_000; nowMs += 100)
		{
			if (policy.TryBeginSend(Peer, nowMs))
			{
				policy.RecordFailure(Peer, SteamSendFailureKind.QueueFull, nowMs);
			}
		}

		var stall = Assert.Single(policy.CollectStallEscalations(31_000));
		Assert.Equal(Peer, stall.PeerId);
		Assert.True(stall.CongestedForMs >= PeerSendRefusalPolicy.DefaultStallMs);
		Assert.True(stall.RefusedAttempts > 0);

		// Once per episode, however often the caller asks.
		Assert.Empty(policy.CollectStallEscalations(31_000));
		Assert.Empty(policy.CollectStallEscalations(40_000));
	}

	[Fact]
	public void LapsedEpisode_DoesNotEscalate_AndTheNextRefusalStartsFresh()
	{
		var policy = new PeerSendRefusalPolicy();

		policy.RecordFailure(Peer, SteamSendFailureKind.QueueFull, 0);

		// Nothing was attempted for 40 s: the peer is idle, not "still refusing".
		Assert.Empty(policy.CollectStallEscalations(40_000));

		// The next refusal starts a new episode instead of inheriting the old one,
		// so it cannot escalate instantly.
		Assert.True(policy.RecordFailure(Peer, SteamSendFailureKind.QueueFull, 40_000));
		Assert.Empty(policy.CollectStallEscalations(40_001));
	}

	[Fact]
	public void PeersAreIsolated_AndResetClearsEverything()
	{
		var policy = new PeerSendRefusalPolicy();

		policy.RecordFailure(Peer, SteamSendFailureKind.QueueFull, 0);
		Assert.False(policy.TryBeginSend(Peer, 10));
		Assert.True(policy.TryBeginSend(OtherPeer, 10));

		policy.Reset();

		Assert.True(policy.TryBeginSend(Peer, 11));
		Assert.Empty(policy.CollectDueReports(1_000_000));
		Assert.Empty(policy.CollectStallEscalations(1_000_000));
	}

	[Fact]
	public void Reset_DropsThePendingWindow_SoNoStaleCountReachesTheNextSession()
	{
		var policy = new PeerSendRefusalPolicy();

		// A session ends mid-window: two refusals and one suppressed attempt are
		// still pending when Reset arrives.
		policy.RecordFailure(Peer, SteamSendFailureKind.QueueFull, 0);
		policy.RecordFailure(Peer, SteamSendFailureKind.QueueFull, 250);
		Assert.False(policy.TryBeginSend(Peer, 300));

		policy.Reset();

		// Eight minutes later the next session's first send is not gated, and the
		// first report counts only what the new session did.
		Assert.True(policy.TryBeginSend(Peer, 500_000));
		Assert.True(policy.RecordFailure(Peer, SteamSendFailureKind.QueueFull, 500_000));
		var report = Assert.Single(policy.CollectDueReports(505_000));
		Assert.Equal(1, report.RefusedAttempts);
		Assert.Equal(0, report.SuppressedAttempts);
		Assert.Equal(5_000L, report.WindowMs);
	}

	[Fact]
	public void BackwardsClockJump_DropsStoredDeadlines_InsteadOfGatingForever()
	{
		var policy = new PeerSendRefusalPolicy();

		// Gated just before Environment.TickCount wraps: the deadline is written in
		// the old epoch (a large positive reading).
		Assert.True(policy.RecordFailure(Peer, SteamSendFailureKind.QueueFull, 2_147_483_000));

		// After the wrap the clock reads negative. The old deadline must not be read
		// as an enormous remaining delay: the peer is sendable again at once, no
		// report counts the old epoch and nothing escalates.
		Assert.True(policy.TryBeginSend(Peer, -2_147_483_000));
		Assert.Empty(policy.CollectDueReports(-2_147_483_000));
		Assert.Empty(policy.CollectStallEscalations(-2_147_483_000));
	}
}
