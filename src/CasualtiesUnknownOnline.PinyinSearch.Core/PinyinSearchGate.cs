using BepInEx.Logging;
using CasualtiesUnknownOnline.PinyinSearch.Core.Search;

namespace CasualtiesUnknownOnline.PinyinSearch.Core;

/// <summary>
/// The switch and the log sink the static patch classes read. A static Harmony
/// patch class cannot receive constructor injection, so the plug-in shell binds
/// the log source once in Awake — the same discipline CUO's own patch gates
/// use. The switch itself is not copied: it is read live from
/// <see cref="PinyinSearchConfig"/> on every query.
/// </summary>
internal static class PinyinSearchGate
{
	/// <summary>Whether a pinyin-enabled surface may extend its native matching right now.</summary>
	internal static bool Enabled => PinyinSearchConfig.IsEnabled;

	/// <summary>The mod's log source, so the static patch classes can report their own key paths.</summary>
	internal static ManualLogSource? Log { get; private set; }

	internal static void Bind(ManualLogSource log) => Log = log;

	/// <summary>
	/// Reports the reading table once, when the first search actually needs it
	/// (low frequency → Information). A table that failed to embed must be
	/// visible in the log: pinyin search would otherwise look enabled while
	/// silently matching nothing. The console's completion stage reports through
	/// the same helper, so a missing table produces one line, not one per
	/// surface.
	/// </summary>
	internal static void ReportTableOnce()
	{
		if (Log is not null)
		{
			PinyinTableReport.ReportOnce(Log);
		}
	}
}
