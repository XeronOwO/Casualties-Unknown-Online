using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Per-peer exponential backoff for the host's P2P warm-up pings. The warm-up
/// pump must keep trying while a Steam P2P session establishes (the first
/// messages are frequently swallowed), but a peer whose session is broken or
/// already gone would otherwise be pinged every retry interval forever —
/// each failure is a Steam `k_EResultConnectFailed` warning (the observed
/// "offline-member P2P noise": ~40 warnings per minute). Failed sends back
/// off 1 s → 2 s → 4 s → … up to the cap; one successful send resets the
/// peer to the initial interval, so a recovering session is never left
/// waiting on an old failure streak. Pure decision machine — all time comes
/// in from the caller (the session's <c>ITimeSource.NowMs</c>), no wall
/// clock and no transport dependency, which keeps the policy L0-testable.
/// </summary>
public sealed class PeerWarmupBackoff
{
	public const long DefaultInitialDelayMs = 1_000;
	public const long DefaultMaxDelayMs = 10_000;

	private readonly Dictionary<ulong, PeerState> _peers = [];
	private readonly long _initialDelayMs;
	private readonly long _maxDelayMs;

	public PeerWarmupBackoff(long initialDelayMs = DefaultInitialDelayMs, long maxDelayMs = DefaultMaxDelayMs)
	{
		_initialDelayMs = Math.Max(1, initialDelayMs);
		_maxDelayMs = Math.Max(_initialDelayMs, maxDelayMs);
	}

	/// <summary>True when the peer is due for another warm-up send.</summary>
	public bool ShouldSend(ulong steamId, long nowMs) =>
		!_peers.TryGetValue(steamId, out var state) || nowMs >= state.NextAttemptMs;

	/// <summary>A send failed — schedule the peer's next attempt one doubled delay later (capped).</summary>
	public void RecordFailure(ulong steamId, long nowMs)
	{
		if (_peers.TryGetValue(steamId, out var state))
		{
			state.DelayMs = Math.Min(_maxDelayMs, state.DelayMs * 2);
		}
		else
		{
			state = new PeerState { DelayMs = _initialDelayMs };
			_peers[steamId] = state;
		}

		state.NextAttemptMs = nowMs + state.DelayMs;
	}

	/// <summary>A send succeeded — the peer is reachable again: drop its failure streak.</summary>
	public void RecordSuccess(ulong steamId) => _peers.Remove(steamId);

	/// <summary>The lobby identity changed — no failure history crosses into the next lobby.</summary>
	public void Reset() => _peers.Clear();

	private sealed class PeerState
	{
		public long DelayMs;
		public long NextAttemptMs;
	}
}
