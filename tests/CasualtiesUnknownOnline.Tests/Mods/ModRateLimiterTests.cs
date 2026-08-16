using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The token bucket behind the mod-message and command-request rate limits:
/// burst capacity is available immediately, the sustained rate refills over
/// time, and an empty bucket refuses frames instead of queuing them.
/// </summary>
public class ModRateLimiterTests
{
	[Fact]
	public void Burst_IsAvailableImmediately()
	{
		var limiter = new ModRateLimiter(refillPerSecond: 10, burst: 3);

		Assert.True(limiter.TryConsume(1000));
		Assert.True(limiter.TryConsume(1000));
		Assert.True(limiter.TryConsume(1000));
		Assert.False(limiter.TryConsume(1000), "the burst is exhausted");
	}

	[Fact]
	public void SustainedRate_RefillsOverTime()
	{
		var limiter = new ModRateLimiter(refillPerSecond: 10, burst: 2);

		Assert.True(limiter.TryConsume(0));
		Assert.True(limiter.TryConsume(0));
		Assert.False(limiter.TryConsume(0));
		Assert.True(limiter.TryConsume(100), "100 ms at 10/s refills exactly one token");
		Assert.False(limiter.TryConsume(101), "only 1 ms has passed");
	}

	[Fact]
	public void Refill_NeverExceedsBurst()
	{
		var limiter = new ModRateLimiter(refillPerSecond: 10, burst: 2);

		Assert.True(limiter.TryConsume(0));
		Assert.True(limiter.TryConsume(0));
		Assert.False(limiter.TryConsume(0));
		Assert.True(limiter.TryConsume(10_000), "a long idle refills at most the burst");
		Assert.True(limiter.TryConsume(10_000));
		Assert.False(limiter.TryConsume(10_000));
	}

	[Fact]
	public void ClockGoingBackwards_DoesNotRefillOrThrow()
	{
		var limiter = new ModRateLimiter(refillPerSecond: 10, burst: 2);

		limiter.TryConsume(1000);
		limiter.TryConsume(1000);

		Assert.False(limiter.TryConsume(500), "a backwards clock must not fabricate tokens");
	}
}
