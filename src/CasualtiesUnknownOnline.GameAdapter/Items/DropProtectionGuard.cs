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
///
/// The short window is not enough for one family: a GUEST's block-break drops
/// exist only on this side until the host answers the break report that carries
/// them, and the re-report fallback's window (5 s at its densest, 60 s steady)
/// is far longer than this
/// guard's. Those items are therefore protected by the report state itself
/// (<paramref name="isPendingBreakDrop"/>, the Runtime's pending break-drop
/// table) for as long as they stay unacknowledged — killing one would destroy an
/// item the host has never heard of, which is exactly the divergence the drop
/// recovery exists to prevent.
/// </summary>
internal sealed class DropProtectionGuard(Func<ulong, bool>? isPendingBreakDrop = null)
{
	private const int ProtectMs = 400;

	private readonly Dictionary<ulong, long> _until = [];
	private readonly Func<ulong, bool>? _isPendingBreakDrop = isPendingBreakDrop;

	internal void Mark(ulong itemId) => _until[itemId] = Environment.TickCount + ProtectMs;

	internal bool IsProtected(ulong itemId) =>
		(_until.TryGetValue(itemId, out var until) && until > Environment.TickCount)
		|| (_isPendingBreakDrop?.Invoke(itemId) ?? false);

	internal void Remove(ulong itemId) => _until.Remove(itemId);
}
