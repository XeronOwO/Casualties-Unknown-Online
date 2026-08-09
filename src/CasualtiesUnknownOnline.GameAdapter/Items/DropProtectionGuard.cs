using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Snapshot-race guard for freshly dropped/materialized world items: a fresh
/// item registered AFTER the periodic keyframe was generated is not in it yet —
/// the reconcile must not kill it, or the kill → destroy report → table delete
/// → next-keyframe-misses → kill loop eats it forever ("an item disappears").
/// Marked by the report side (ItemWorldSync: local drops, remote
/// materializations), consulted by the reconcile (ItemApplication) and pruned
/// by the follow (ItemPositionFollow). A tiny dedicated owner keeps the
/// construction graph acyclic — neither side needs to know the other.
/// </summary>
internal sealed class DropProtectionGuard
{
	private const int ProtectMs = 400;

	private readonly Dictionary<ulong, long> _until = [];

	internal void Mark(ulong itemId) => _until[itemId] = Environment.TickCount + ProtectMs;

	internal bool IsProtected(ulong itemId) =>
		_until.TryGetValue(itemId, out var until) && until > Environment.TickCount;

	internal void Remove(ulong itemId) => _until.Remove(itemId);
}
