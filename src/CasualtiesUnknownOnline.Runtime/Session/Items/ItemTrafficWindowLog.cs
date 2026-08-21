using System;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>Formats one <see cref="ItemTrafficWindow"/> for the periodic log.</summary>
internal static class ItemTrafficWindowLog
{
	internal static string Format(ItemTrafficWindow window) =>
		$"total {window.Total}; " +
		string.Join("; ", Enum.GetValues(typeof(ItemTrafficKind))
			.Cast<ItemTrafficKind>()
			.Select(kind => $"{kind}={window.CountFor(kind)}")) +
		(window.TopItems.Count == 0
			? ""
			: "; top: " + string.Join(", ", window.TopItems.Select(b => $"{b.ItemId}×{b.Count}")));
}
