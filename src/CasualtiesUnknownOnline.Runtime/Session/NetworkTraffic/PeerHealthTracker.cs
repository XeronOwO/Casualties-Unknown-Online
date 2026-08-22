using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;

/// <summary>
/// Pure per-peer diagnostics for the ping/pong probe loop: rolling RTT samples,
/// jitter (packet-delay variation), probe loss and completed/lost counters.
/// Probes are matched by the ping's original tick stamp, so a late pong from an
/// already-lost probe can never be mistaken for the current outstanding probe.
/// It owns no transport/session state and is fed only by
/// <see cref="NetworkTrafficMonitor"/> from the session's ping callbacks.
/// This is observability-only — no route, retry or rate-limit decision is made
/// from these numbers.
/// </summary>
internal sealed class PeerHealthTracker
{
	private const int MaxRttSamples = 16;

	private readonly Dictionary<ulong, PeerHealthState> _peers = [];

	internal void RecordPingSent(ulong peer, long sendTicks, long nowMs)
	{
		var state = GetOrCreate(peer);
		if (state.PendingPingSentMs >= 0)
		{
			// The previous probe was still unanswered when the next probe went
			// out — count it as lost (the reliable Steam channel retransmits, so
			// this is a probe-loss / peer-reachability signal, not raw packet loss).
			state.PingsLost++;
		}

		state.PendingPingTicks = sendTicks;
		state.PendingPingSentMs = nowMs;
		state.PingsSent++;
	}

	internal void RecordPong(ulong peer, float rttMs, long echoTicks)
	{
		var state = GetOrCreate(peer);
		state.LastRttMs = rttMs;
		if (state.PendingPingSentMs < 0 || state.PendingPingTicks != echoTicks)
		{
			return; // duplicate/late pong for an already-closed or different probe — keep the RTT but no sample double-count
		}

		state.PendingPingSentMs = -1;
		state.PendingPingTicks = -1;
		state.PingsCompleted++;
		state.RttSamples.Add(rttMs);
		if (state.RttSamples.Count > MaxRttSamples)
		{
			state.RttSamples.RemoveAt(0);
		}

		if (state.RttSamples.Count >= 2)
		{
			state.JitterMs = Math.Abs(
				state.RttSamples[state.RttSamples.Count - 1]
				- state.RttSamples[state.RttSamples.Count - 2]);
		}

		state.AverageRttMs = state.RttSamples.Average();
	}

	internal bool TryGetSnapshot(ulong peer, out PeerHealthSnapshot snapshot)
	{
		if (_peers.TryGetValue(peer, out var state))
		{
			snapshot = BuildSnapshot(state);
			return true;
		}

		snapshot = null!;
		return false;
	}

	internal IReadOnlyList<PeerHealthSnapshot> Snapshots() =>
		[.. _peers.Select(kv => BuildSnapshot(kv.Value)).OrderBy(x => x.SteamId)];

	internal void Reset() => _peers.Clear();

	private PeerHealthState GetOrCreate(ulong peer)
	{
		if (!_peers.TryGetValue(peer, out var state))
		{
			state = new PeerHealthState { SteamId = peer };
			_peers[peer] = state;
		}

		return state;
	}

	private static PeerHealthSnapshot BuildSnapshot(PeerHealthState state)
	{
		var denominator = state.PingsCompleted + state.PingsLost;
		var lossPercent = denominator == 0 ? 0f : state.PingsLost * 100f / denominator;
		return new PeerHealthSnapshot(
			state.SteamId,
			state.LastRttMs,
			state.AverageRttMs,
			state.JitterMs,
			state.PingsSent,
			state.PingsCompleted,
			state.PingsLost,
			lossPercent);
	}

	internal sealed record PeerHealthSnapshot(
		ulong SteamId,
		float LastRttMs,
		float AverageRttMs,
		float JitterMs,
		int PingsSent,
		int PingsCompleted,
		int PingsLost,
		float LossPercent);

	private sealed class PeerHealthState
	{
		public ulong SteamId;
		public long PendingPingTicks = -1;
		public long PendingPingSentMs = -1;
		public float LastRttMs = -1f;
		public float AverageRttMs = -1f;
		public float JitterMs;
		public int PingsSent;
		public int PingsCompleted;
		public int PingsLost;
		public List<float> RttSamples = [];
	}
}
