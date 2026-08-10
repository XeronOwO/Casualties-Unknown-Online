using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The authoritative world-item table: instance id → item. Recorded on the
/// host and in solo play (Role != Guest — a solo-turned-lobby host keeps its
/// table so a late joiner sees the same world), broadcast only while the
/// session is active. The table state belongs here (user rule: state belongs
/// to its owner) — ItemService owns the sync semantics (arbitration, reports,
/// broadcasts) and talks to the table through these narrow operations.
/// Split out of ItemService when the 600-line gate demanded it.
/// </summary>
internal sealed class WorldItemTable
{
	private readonly Dictionary<ulong, WorldItem> _items = [];

	/// <summary>Read-only view — the snapshot service and the arbitration iterate it.</summary>
	internal IReadOnlyDictionary<ulong, WorldItem> Items => _items;

	/// <summary>Register or overwrite (a drop report re-positions an entry).</summary>
	internal void Set(ulong itemId, WorldItem item) => _items[itemId] = item;

	/// <summary>Register only when absent (a spawn race, an idempotent retransmit) — false when the entry already exists.</summary>
	internal bool RegisterIfAbsent(ulong itemId, WorldItem item)
	{
		if (_items.ContainsKey(itemId))
		{
			return false;
		}

		_items[itemId] = item;
		return true;
	}

	internal bool TryGetValue(ulong itemId, out WorldItem item) => _items.TryGetValue(itemId, out item!);

	internal bool ContainsKey(ulong itemId) => _items.ContainsKey(itemId);

	internal void Remove(ulong itemId) => _items.Remove(itemId);

	internal void Clear() => _items.Clear();
}
