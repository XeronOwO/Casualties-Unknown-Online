using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The in-process message bus the fake transports live on: peer registry,
/// virtual clock, per-link fault injection (down / delay / unreliable drop /
/// duplicate). Deterministic — the Random seed is fixed by the test.
/// </summary>
internal sealed class FakeNetwork
{
	private readonly Dictionary<ulong, FakeTransport> _peers = [];
	private readonly List<QueuedMessage> _queued = [];
	private readonly Dictionary<(ulong From, ulong To), LinkFaults> _faults = [];
	private readonly Random _random;
	private readonly FakeClock _clock;

	internal FakeNetwork(int seed = 12345, FakeClock? clock = null)
	{
		_random = new Random(seed);
		_clock = clock ?? new FakeClock();
	}

	/// <summary>The virtual now — the network's schedule and the domain services' clocks are one and the same when a FakeClock is shared.</summary>
	internal long NowMs => _clock.NowMs;

	internal void Register(FakeTransport peer) => _peers[peer.SteamId] = peer;

	internal void Unregister(ulong steamId) => _peers.Remove(steamId);

	internal void SetFaults(ulong from, ulong to, LinkFaults faults) => _faults[(from, to)] = faults;

	/// <summary>Clear the injected faults on a link (the connection healed).</summary>
	internal void ClearFaults(ulong from, ulong to) => _faults.Remove((from, to));

	/// <summary>Advance the virtual clock and deliver every message that came due.</summary>
	internal void Advance(long ms)
	{
		_clock.Advance(ms);
		FlushDue();
	}

	/// <summary>
	/// Route one frame from peer to peer. Mirrors SteamTransport.SendTo:
	/// returns false when the link is down (or the peer is gone), true
	/// otherwise — a dropped unreliable message still reports a successful
	/// send (the sender never learns, exactly like Steam).
	/// </summary>
	internal bool Route(ulong from, ulong to, byte[] data, bool reliable)
	{
		if (!_peers.ContainsKey(to))
		{
			return false;
		}

		_faults.TryGetValue((from, to), out var faults);
		if (faults?.Down == true)
		{
			return false;
		}

		if (!reliable && faults?.UnreliableDropRate > 0 && _random.NextDouble() < faults.UnreliableDropRate)
		{
			return true; // sent, never delivered — unreliable loss
		}

		var deliverAt = faults?.DelayMs > 0 ? _clock.NowMs + faults.DelayMs : _clock.NowMs;
		Queue(deliverAt, from, to, data);
		if (faults?.Duplicate == true)
		{
			Queue(deliverAt, from, to, data); // retransmission-style duplicate
		}

		if (faults?.DelayMs == 0 || faults is null)
		{
			FlushDue(); // no delay: deliver synchronously, in queue order
		}

		return true;
	}

	/// <summary>Direct delivery — tests use this to inject frames manually (reorder, duplicate).</summary>
	internal void Deliver(ulong from, ulong to, byte[] data)
	{
		if (_peers.TryGetValue(to, out var peer))
		{
			peer.Deliver(from, data);
		}
	}

	private void Queue(long deliverAtMs, ulong from, ulong to, byte[] data) =>
		_queued.Add(new QueuedMessage(deliverAtMs, from, to, data));

	private void FlushDue()
	{
		// Deliver in DUE order (earliest deliverAt first), NOT queue order — a
		// message with an earlier deliverAt that was queued later must arrive
		// before a later-due one queued earlier (the real network delivers in
		// arrival order; a single queue-order scan delivers the later-due one
		// first when both became due by the flush's clock). Messages with the
		// SAME deliverAt arrive in send order (the reliable channel's FIFO).
		// Found by the reordered-arrival race test: sender order ≠ arrival
		// order only holds when due order wins.
		while (true)
		{
			var earliest = long.MaxValue;
			for (var i = 0; i < _queued.Count; i++)
			{
				if (_queued[i].DeliverAtMs <= _clock.NowMs && _queued[i].DeliverAtMs < earliest)
				{
					earliest = _queued[i].DeliverAtMs;
				}
			}

			if (earliest == long.MaxValue)
			{
				return;
			}

			for (var i = 0; i < _queued.Count; i++)
			{
				if (_queued[i].DeliverAtMs == earliest)
				{
					var msg = _queued[i];
					_queued.RemoveAt(i--);
					Deliver(msg.From, msg.To, msg.Data);
				}
			}
		}
	}

	private readonly struct QueuedMessage
	{
		internal QueuedMessage(long deliverAtMs, ulong from, ulong to, byte[] data)
		{
			DeliverAtMs = deliverAtMs;
			From = from;
			To = to;
			Data = data;
		}

		internal long DeliverAtMs { get; }

		internal ulong From { get; }

		internal ulong To { get; }

		internal byte[] Data { get; }
	}
}

/// <summary>Per-link fault injection knobs.</summary>
internal sealed class LinkFaults
{
	internal bool Down { get; set; } // link broken: SendTo returns false

	internal long DelayMs { get; set; } // virtual-clock delivery delay

	internal double UnreliableDropRate { get; set; } // 0-1, unreliable messages only

	internal bool Duplicate { get; set; } // deliver twice (retransmission style)
}
