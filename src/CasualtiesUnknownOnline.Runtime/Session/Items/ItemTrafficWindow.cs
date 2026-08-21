using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Immutable snapshot of one item-traffic observation window. The window is
/// deliberately small: the UI/log only needs totals, per-kind splits and the
/// noisiest item labels to decide whether a high-frequency family needs
/// batching/rate limiting.
/// </summary>
internal sealed class ItemTrafficWindow
{
	private readonly IReadOnlyDictionary<ItemTrafficKind, int> _byKind;

	internal ItemTrafficWindow(
		long startMs,
		long endMs,
		long total,
		IReadOnlyDictionary<ItemTrafficKind, int> byKind,
		IReadOnlyList<ItemTrafficBucket> topItems)
	{
		StartMs = startMs;
		EndMs = endMs;
		Total = total;
		_byKind = byKind;
		TopItems = topItems;
	}

	internal long StartMs { get; }

	internal long EndMs { get; }

	internal long Total { get; }

	internal IReadOnlyDictionary<ItemTrafficKind, int> ByKind => _byKind;

	internal IReadOnlyList<ItemTrafficBucket> TopItems { get; }

	internal int CountFor(ItemTrafficKind kind) =>
		_byKind.TryGetValue(kind, out var count) ? count : 0;
}
