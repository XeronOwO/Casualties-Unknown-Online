using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// Folds the adapter's candidate rows into the bounded census the probe reports. The adapter reports
/// one row per object it found — duplicates included — and this is where that becomes readable: equal
/// styles collapse into one row carrying how often it occurs, the most common come first, and the
/// result is capped so one busy screen cannot flood the log. Order and cap are the whole contract,
/// which is why this is a pure function with its own cases instead of a loop in the adapter.
/// </summary>
public static class OnlineUiNativeStyleCensus
{
	/// <summary>How many distinct styles one census may report.</summary>
	public const int DefaultCap = 40;

	/// <summary>The bounded census of image styles: folded, ordered by frequency and then by name so the
	/// same screen always produces the same lines.</summary>
	public static IReadOnlyList<OnlineUiNativeImageStyle> CollapseImageStyles(
		IEnumerable<OnlineUiNativeImageStyle> candidates,
		int cap = DefaultCap) =>
		cap <= 0
			? []
			: [.. candidates
				.GroupBy(style => (
					style.Source,
					style.SpriteName,
					style.ImageType,
					style.PixelsPerUnitMultiplier,
					style.BorderLeft,
					style.BorderBottom,
					style.BorderRight,
					style.BorderTop,
					style.Color))
				.Select(group => group.First() with { Occurrences = group.Count() })
				.OrderByDescending(style => style.Occurrences)
				.ThenBy(style => style.SpriteName, StringComparer.Ordinal)
				.ThenBy(style => style.ObjectPath, StringComparer.Ordinal)
				.Take(cap)];

	/// <summary>The same fold for text styles: the font, the size and the colour identify a style, and the
	/// first object that carried it names where the probe saw it.</summary>
	public static IReadOnlyList<OnlineUiNativeTextStyle> CollapseTextStyles(
		IEnumerable<OnlineUiNativeTextStyle> candidates,
		int cap = DefaultCap) =>
		cap <= 0
			? []
			: [.. candidates
				.GroupBy(style => (style.Source, style.FontAssetName, style.FontSize, style.Color))
				.Select(group => group.First() with { Occurrences = group.Count() })
				.OrderByDescending(style => style.Occurrences)
				.ThenBy(style => style.FontAssetName, StringComparer.Ordinal)
				.ThenBy(style => style.ObjectPath, StringComparer.Ordinal)
				.Take(cap)];
}
