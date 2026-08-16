using System;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// A token bucket for one sender: <paramref name="refillPerSecond"/> tokens
/// accrue continuously up to <paramref name="burst"/>, and
/// <see cref="TryConsume"/> spends one token per admitted frame. Pure state
/// machine — the clock is passed in, so the test suite drives it with the
/// virtual FakeClock exactly like every other throttle in the runtime.
/// </summary>
public sealed class ModRateLimiter(int refillPerSecond, int burst)
{
	private readonly int _refillPerSecond = refillPerSecond;
	private readonly int _burst = burst;
	private double _tokens;
	private long _lastMs = long.MinValue;

	/// <summary>True and spends one token when the bucket has capacity at <paramref name="nowMs"/>; false otherwise (the caller drops the frame).</summary>
	public bool TryConsume(long nowMs)
	{
		if (_lastMs == long.MinValue)
		{
			_tokens = _burst;
			_lastMs = nowMs;
		}
		else
		{
			var elapsed = Math.Max(0, nowMs - _lastMs);
			_tokens = Math.Min(_burst, _tokens + (elapsed * _refillPerSecond / 1000.0));
			_lastMs = nowMs;
		}

		if (_tokens < 1)
		{
			return false;
		}

		_tokens -= 1;
		return true;
	}
}
