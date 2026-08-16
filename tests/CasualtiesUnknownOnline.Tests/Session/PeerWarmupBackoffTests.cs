using CasualtiesUnknownOnline.Runtime.Session;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The pure per-peer warm-up backoff: failures double the retry delay up to
/// the cap, success resets the streak, and peers never share state. This is
/// the decision machine behind the host's P2P warm-up pump — the L0 slice
/// that used to be buried inside SessionService's fixed 1 s retry loop.
/// </summary>
public class PeerWarmupBackoffTests
{
	private const ulong PeerA = 1001;
	private const ulong PeerB = 2001;

	[Fact]
	public void FirstAttempt_IsAlwaysDue()
	{
		var backoff = new PeerWarmupBackoff();

		Assert.True(backoff.ShouldSend(PeerA, 0), "a peer with no failure history must be due immediately");
	}

	[Fact]
	public void Failures_BackOffExponentially_UpToTheCap()
	{
		var backoff = new PeerWarmupBackoff();

		// Send at t=0 fails -> next due at +1 s.
		Assert.True(backoff.ShouldSend(PeerA, 0));
		backoff.RecordFailure(PeerA, 0);
		Assert.False(backoff.ShouldSend(PeerA, 999));
		Assert.True(backoff.ShouldSend(PeerA, 1000));

		// Fail again -> +2 s; +4 s; +8 s; then the 10 s cap.
		backoff.RecordFailure(PeerA, 1000);
		Assert.False(backoff.ShouldSend(PeerA, 2999));
		Assert.True(backoff.ShouldSend(PeerA, 3000));

		backoff.RecordFailure(PeerA, 3000);
		Assert.False(backoff.ShouldSend(PeerA, 6999));
		Assert.True(backoff.ShouldSend(PeerA, 7000));

		backoff.RecordFailure(PeerA, 7000);
		Assert.False(backoff.ShouldSend(PeerA, 14999));
		Assert.True(backoff.ShouldSend(PeerA, 15000));

		backoff.RecordFailure(PeerA, 15000);
		Assert.False(backoff.ShouldSend(PeerA, 24999));
		Assert.True(backoff.ShouldSend(PeerA, 25000));
	}

	[Fact]
	public void Success_ResetsTheFailureStreak()
	{
		var backoff = new PeerWarmupBackoff();
		backoff.RecordFailure(PeerA, 0);
		backoff.RecordFailure(PeerA, 1000); // next due at 3000

		backoff.RecordSuccess(PeerA);
		Assert.True(backoff.ShouldSend(PeerA, 1001), "a successful send must make the peer due again immediately");

		backoff.RecordFailure(PeerA, 1001);
		Assert.False(backoff.ShouldSend(PeerA, 2000));
		Assert.True(backoff.ShouldSend(PeerA, 2001), "the reset streak restarts at the initial 1 s delay, not the old doubled one");
	}

	[Fact]
	public void Peers_BackOffIndependently()
	{
		var backoff = new PeerWarmupBackoff();
		backoff.RecordFailure(PeerA, 0); // PeerA next due at 1000
		backoff.RecordFailure(PeerA, 1000);
		backoff.RecordFailure(PeerA, 3000); // PeerA next due at 7000

		Assert.True(backoff.ShouldSend(PeerB, 0), "a peer without failures is unaffected by another peer's streak");
		Assert.False(backoff.ShouldSend(PeerA, 6000));
		Assert.True(backoff.ShouldSend(PeerB, 6000));
	}

	[Fact]
	public void Reset_ClearsEveryPeer()
	{
		var backoff = new PeerWarmupBackoff();
		backoff.RecordFailure(PeerA, 0);
		backoff.RecordFailure(PeerB, 0);

		backoff.Reset();

		Assert.True(backoff.ShouldSend(PeerA, 0), "after a lobby change every peer must start with a clean slate");
		Assert.True(backoff.ShouldSend(PeerB, 0));
	}
}
