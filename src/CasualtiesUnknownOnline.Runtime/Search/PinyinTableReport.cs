using System.Threading;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Search;

/// <summary>
/// The one process-wide report of the embedded reading table's state. Pinyin
/// search has two surfaces — the Game Adapter's crafting patch and the console's
/// completion stage — and a table that failed to embed must be visible once, not
/// once per surface: both call this report, so the log carries a single sentence
/// no matter which surface is used first.
///
/// A missing table degrades every surface to the literal substring rule instead
/// of failing, so silence here would hide a real degradation.
/// </summary>
public static class PinyinTableReport
{
	private static int _reported;

	/// <summary>
	/// Reports the table's state on the first call (Information when loaded,
	/// warning when it is missing) and does nothing afterwards.
	/// </summary>
	public static void ReportOnce(ILogger log)
	{
		if (Interlocked.Exchange(ref _reported, 1) == 1)
		{
			return;
		}

		var count = PinyinDictionary.Count;
		if (count == 0)
		{
			log.LogWarning(
				"[Pinyin] the embedded reading table is missing — pinyin search is inactive and every surface keeps the literal substring rule.");
			return;
		}

		log.LogInformation("[Pinyin] search enabled — {Count} characters loaded.", count);
	}
}
