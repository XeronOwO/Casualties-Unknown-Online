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
	private long _nowMs;

	internal FakeNetwork(int seed = 12345)
	{
		_random = new Random(seed);
	}

	internal long NowMs => _nowMs;

	internal void Register(FakeTransport peer) => _peers[peer.SteamId] = peer;

	internal void Unregister(ulong steamId) => _peers.Remove(steamId);

	internal void SetFaults(ulong from, ulong to, LinkFaults faults) => _faults[(from, to)] = faults;

	/// <summary>Advance the virtual clock and deliver every message that came due.</summary>
	internal void Advance(long ms)
	{
		_nowMs += ms;
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

		var deliverAt = faults?.DelayMs > 0 ? _nowMs + faults.DelayMs : _nowMs;
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
		// FIFO per instant: same-instant messages arrive in send order (the
		// reliable channel's ordering; different DelayMs values produce reorder).
		for (var i = 0; i < _queued.Count; i++)
		{
			var msg = _queued[i];
			if (msg.DeliverAtMs <= _nowMs)
			{
				_queued.RemoveAt(i--);
				Deliver(msg.From, msg.To, msg.Data);
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
