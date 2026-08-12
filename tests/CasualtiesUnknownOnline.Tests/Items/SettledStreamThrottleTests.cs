using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The host's position-stream throttle (SettledStreamThrottle): a settled item
/// re-aligns at 1 Hz (every 10th pump) instead of every 10 Hz tick, EXCEPT the
/// motion→rest edge which forces one immediate tick — the guest's copy stops
/// by itself but its final resting spot must converge on the authority's, and
/// waiting for the round would leave the end state open for up to a second.
/// </summary>
public class SettledStreamThrottleTests
{
	private const ulong Item = 42;
	private const ulong Other = 43;

	private static bool Pump(SettledStreamThrottle throttle, bool settled)
	{
		throttle.BeginPump();
		return throttle.ShouldSend(Item, settled);
	}

	[Fact]
	public void MovingItem_AlwaysSent()
	{
		var throttle = new SettledStreamThrottle();

		for (var i = 0; i < 25; i++)
		{
			Assert.True(Pump(throttle, settled: false));
		}
	}

	[Fact]
	public void SettledEdge_SentImmediately()
	{
		var throttle = new SettledStreamThrottle();
		Pump(throttle, settled: false); // was moving

		Assert.True(Pump(throttle, settled: true), "the motion→rest edge forces one immediate tick");
	}

	[Fact]
	public void SettledContinuation_SkippedUntilRound()
	{
		var throttle = new SettledStreamThrottle();
		Pump(throttle, settled: false); // pump 1 — moving
		Pump(throttle, settled: true); // pump 2 — the edge, sent

		// Pumps 3..9: no edge, no round — skipped (the 10th pump rounds).
		for (var i = 0; i < 7; i++)
		{
			Assert.False(Pump(throttle, settled: true), $"pump {i + 3}: settled non-edge outside the round must not send");
		}
	}

	[Fact]
	public void SettledRound_SentEveryTenthPump()
	{
		var throttle = new SettledStreamThrottle();
		Pump(throttle, settled: false);
		Pump(throttle, settled: true); // the edge

		// Pumps 3..10: the 10th is the round (tick 10 % 10 == 0) — sent.
		for (var i = 0; i < 7; i++)
		{
			Assert.False(Pump(throttle, settled: true));
		}

		Assert.True(Pump(throttle, settled: true), "the 1 Hz round re-aligns a settled item");
		Assert.False(Pump(throttle, settled: true), "pump 11 is not a round");
	}

	[Fact]
	public void MovingAgain_ResetsTheEdge()
	{
		var throttle = new SettledStreamThrottle();
		Pump(throttle, settled: false);
		Pump(throttle, settled: true); // the edge
		Pump(throttle, settled: true); // skipped

		Assert.True(Pump(throttle, settled: false), "a moving item always sends");
		Assert.True(Pump(throttle, settled: true), "settled again = a NEW edge — one immediate tick");
		Assert.False(Pump(throttle, settled: true), "then back to the round cadence");
	}

	[Fact]
	public void Items_Isolated()
	{
		var throttle = new SettledStreamThrottle();
		throttle.BeginPump();
		Assert.True(throttle.ShouldSend(Item, settled: true), "A's edge");
		Assert.True(throttle.ShouldSend(Other, settled: true), "B's OWN edge (each item's first settled send)");
		throttle.BeginPump();
		Assert.False(throttle.ShouldSend(Item, settled: true), "A settled, non-edge, non-round — skipped");
		Assert.True(throttle.ShouldSend(Other, settled: false), "B moving — sent");
	}

	[Fact]
	public void SingleSettledItem_EdgeThenRoundCadence()
	{
		// A lone settled item (no moving preamble): edge → skipped until the 10th pump.
		var throttle = new SettledStreamThrottle();
		Assert.True(Pump(throttle, settled: true), "first pump = the edge, sent");
		for (var i = 0; i < 8; i++)
		{
			Assert.False(Pump(throttle, settled: true));
		}

		Assert.True(Pump(throttle, settled: true), "the 10th pump is the round");
	}

	[Fact]
	public void Round_IsGlobal_NotPerItem()
	{
		// The round flag is computed once per pump — every settled item rides it.
		var throttle = new SettledStreamThrottle();
		for (var i = 0; i < 10; i++)
		{
			throttle.BeginPump();
		}

		Assert.True(throttle.ShouldSend(Item, settled: true));
		Assert.True(throttle.ShouldSend(Other, settled: true), "a second settled item on the same round pump is sent too");
	}
}
