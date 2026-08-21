using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Item-traffic observation for <see cref="ItemService"/>: records one logical
/// item-domain send operation and lets <see cref="ItemTrafficPump"/> roll the
/// periodic window. Deliberately observability-only — no batching/rate-limit
/// decision is made until the observed volume says a family actually hurts.
/// </summary>
public sealed partial class ItemService
{
	private readonly ItemTrafficTracker _itemTraffic = new(ItemTrafficTracker.DefaultWindowMs);

	/// <summary>Record one logical item-domain send operation (session only).</summary>
	internal void RecordItemTraffic(ItemTrafficKind kind, string itemLabel)
	{
		if (_session.SessionActive)
		{
			_itemTraffic.Record(kind, itemLabel);
		}
	}

	/// <summary>The per-frame time edge: roll and log a window when one elapsed.</summary>
	internal void PumpItemTraffic(long nowMs)
	{
		if (_itemTraffic.TryCollectWindow(nowMs, out var window) && window.Total > 0)
		{
			_log.LogInformation("[ItemTraffic] {Window}", ItemTrafficWindowLog.Format(window));
		}
	}

	/// <summary>Current (un-rolled) window — test and diagnostics surface.</summary>
	internal ItemTrafficWindow CurrentItemTraffic => _itemTraffic.Snapshot();

	/// <summary>Human-readable label for an item: definition id when the world table has it, else the instance id.</summary>
	internal string ItemTrafficLabel(ulong itemId) =>
		_worldTable.TryGetValue(itemId, out var entry) ? entry.Item.ItemId : $"#{itemId}";

	internal void ResetItemTraffic() => _itemTraffic.Reset();
}
