using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The host-side pending-pickup queue: a pickup report that beats its spawn
/// report is held for a short window instead of being refused immediately, so
/// the usual same-sender reliable-channel reorder self-heals into a normal
/// transfer. PURE state — no sends, no logging, no clock beyond the caller's
/// now: <see cref="ItemService"/> owns the integration decisions and the
/// <see cref="PendingPickupPump"/> owns the per-frame expiry.
/// </summary>
internal sealed class PendingPickupQueue(int holdMs = 500)
{
	internal const int DefaultHoldMs = 500;

	private readonly int _holdMs = holdMs;
	private readonly List<PendingPickup> _entries = [];

	/// <summary>One queued pickup claim (sender + item + the digest evidence it carried).</summary>
	internal sealed record PendingPickup(ulong Sender, ulong ItemId, CharacterItemMsg? Evidence, long QueuedAtMs);

	internal int Count => _entries.Count;

	/// <summary>
	/// Queue an unknown pickup. False when the SAME sender already has the SAME
	/// item queued — the retransmission is silent (queueing it twice would let
	/// one spawn settle two claims of the same picker).
	/// </summary>
	internal bool TryEnqueue(ulong sender, ulong itemId, CharacterItemMsg? evidence, long nowMs)
	{
		if (_entries.Exists(e => e.Sender == sender && e.ItemId == itemId))
		{
			return false;
		}

		_entries.Add(new PendingPickup(sender, itemId, evidence, nowMs));
		return true;
	}

	/// <summary>Take the FIRST queued claim for an item (first-writer-wins when several guests raced it), leaving any later claims queued.</summary>
	internal PendingPickup? TryTakeFirst(ulong itemId)
	{
		var index = _entries.FindIndex(e => e.ItemId == itemId);
		if (index < 0)
		{
			return null;
		}

		var entry = _entries[index];
		_entries.RemoveAt(index);
		return entry;
	}

	/// <summary>Take every remaining queued claim for an item (the losers of the settled race).</summary>
	internal List<PendingPickup> TakeByItem(ulong itemId)
	{
		var taken = _entries.FindAll(e => e.ItemId == itemId);
		_entries.RemoveAll(e => e.ItemId == itemId);
		return taken;
	}

	/// <summary>Take every queued claim matching a predicate (the container-content resolution after a registration).</summary>
	internal List<PendingPickup> TakeWhere(Func<PendingPickup, bool> predicate)
	{
		var taken = _entries.FindAll(e => predicate(e));
		_entries.RemoveAll(e => predicate(e));
		return taken;
	}

	/// <summary>Take every claim whose hold window has elapsed — the pump sends the late rejection.</summary>
	internal List<PendingPickup> TakeExpired(long nowMs)
	{
		var taken = _entries.FindAll(e => nowMs - e.QueuedAtMs >= _holdMs);
		_entries.RemoveAll(e => nowMs - e.QueuedAtMs >= _holdMs);
		return taken;
	}

	internal void Reset() => _entries.Clear();
}
