using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The host's tombstones for item ids whose CREATION it refused: an item a peer
/// created locally and reported, and the host judged dead (a block break that
/// lost first-writer-wins, a spawn command the kernel rejected). The id is
/// never registered, so without this a later operation on it would look exactly
/// like an operation on an item whose creation was never reported at all — and
/// that one is a protocol violation. The tombstone keeps the two facts apart
/// and lets the later operation be refused with the reason the creation died,
/// at once, instead of a window that guesses.
///
/// <para>
/// Bounded (oldest evicted first) so a hostile or buggy peer cannot grow it
/// without limit, and cleared when the session ends: item ids are
/// session-partitioned, so a finished session's tombstones must not refuse the
/// next session's allocations.
/// </para>
/// </summary>
public sealed class RefusedItemCreations
{
	/// <summary>How many refused creations are remembered — far above any real refusal burst, far below a memory concern.</summary>
	public const int Capacity = 256;

	private readonly Dictionary<ulong, RejectionReason> _reasons = [];
	private readonly Queue<ulong> _order = [];

	public int Count => _reasons.Count;

	/// <summary>Remember that this id's creation was refused, with the reason a later operation must be answered.</summary>
	public void Record(ulong itemId, RejectionReason reason)
	{
		if (itemId == 0)
		{
			return;
		}

		if (_reasons.ContainsKey(itemId))
		{
			_reasons[itemId] = reason;
			return;
		}

		while (_order.Count >= Capacity)
		{
			_reasons.Remove(_order.Dequeue());
		}

		_reasons[itemId] = reason;
		_order.Enqueue(itemId);
	}

	public bool TryGet(ulong itemId, out RejectionReason reason) => _reasons.TryGetValue(itemId, out reason);

	/// <summary>The session ended — the ids die with it (a new session re-partitions the id space).</summary>
	public void Reset()
	{
		_reasons.Clear();
		_order.Clear();
	}
}
