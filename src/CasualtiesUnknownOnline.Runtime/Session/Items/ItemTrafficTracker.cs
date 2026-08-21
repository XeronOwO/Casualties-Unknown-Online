using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Pure session-scoped item-traffic counter. It records one logical item-domain
/// send operation (not per-recipient transport frames) and rolls an immutable
/// <see cref="ItemTrafficWindow"/> every <see cref="WindowMs"/>. The pump owns
/// the time edge; this class only owns the counters and the window shape.
/// </summary>
internal sealed class ItemTrafficTracker
{
	internal const long DefaultWindowMs = 10_000;
	internal const int MaxTopItems = 10;

	private readonly long _windowMs;
	private readonly Dictionary<string, int> _perItem = [];
	private readonly int[] _perKind = new int[Enum.GetValues(typeof(ItemTrafficKind)).Length];
	private long _windowStartMs;
	private long _total;

	internal ItemTrafficTracker(long windowMs)
	{
		if (windowMs <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(windowMs), "The traffic window must be positive.");
		}

		_windowMs = windowMs;
		_windowStartMs = 0;
	}

	internal long WindowMs => _windowMs;

	internal long WindowStartMs => _windowStartMs;

	internal void Record(ItemTrafficKind kind, string itemId)
	{
		_perItem.TryGetValue(itemId, out var count);
		_perItem[itemId] = count + 1;
		_perKind[(int)kind]++;
		_total++;
	}

	internal bool TryCollectWindow(long nowMs, out ItemTrafficWindow window)
	{
		if (nowMs - _windowStartMs < _windowMs)
		{
			window = null!;
			return false;
		}

		window = Build(_windowStartMs, nowMs);
		ResetTo(nowMs);
		return true;
	}

	internal ItemTrafficWindow Snapshot() => Build(_windowStartMs, _windowStartMs + _windowMs);

	internal void Reset() => ResetTo(_windowStartMs);

	private ItemTrafficWindow Build(long startMs, long endMs)
	{
		var byKind = new Dictionary<ItemTrafficKind, int>();
		for (var i = 0; i < _perKind.Length; i++)
		{
			if (_perKind[i] > 0)
			{
				byKind[(ItemTrafficKind)i] = _perKind[i];
			}
		}

		var topItems = _perItem
			.OrderByDescending(kv => kv.Value)
			.ThenBy(kv => kv.Key, StringComparer.Ordinal)
			.Take(MaxTopItems)
			.Select(kv => new ItemTrafficBucket(kv.Key, kv.Value))
			.ToList();

		return new ItemTrafficWindow(startMs, endMs, _total, byKind, topItems);
	}

	private void ResetTo(long startMs)
	{
		_perItem.Clear();
		Array.Clear(_perKind, 0, _perKind.Length);
		_total = 0;
		_windowStartMs = startMs;
	}
}
