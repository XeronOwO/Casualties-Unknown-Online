using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The pinyin search switch the static patch classes read. A static patch class
/// cannot receive constructor injection, so the DI-owned GameAdapter binds the
/// options monitor once at construction — the same discipline as
/// <c>PatchBridge</c>. Read live (never copied), so a config edit applies to the
/// next search; unbound means disabled, which is exactly the native behavior.
/// </summary>
internal static class PinyinSearchGate
{
	private static IOptionsMonitor<PinyinSearchOptions>? _options;
	private static ILogger? _log;
	private static bool _reportedTable;

	/// <summary>Whether a pinyin-enabled surface may extend its native matching right now.</summary>
	internal static bool Enabled => _options?.CurrentValue.Enabled ?? false;

	/// <summary>The adapter's logger, so the static patch classes can report their own key paths.</summary>
	internal static ILogger? Log => _log;

	internal static void Bind(IOptionsMonitor<PinyinSearchOptions> options, ILogger log)
	{
		_options = options;
		_log = log;
	}

	internal static void Unbind(IOptionsMonitor<PinyinSearchOptions> options)
	{
		if (ReferenceEquals(_options, options))
		{
			_options = null;
		}
	}

	/// <summary>
	/// Reports the reading table once, when the first search actually needs it
	/// (low frequency → Information). A table that failed to embed must be
	/// visible in the log: pinyin search would otherwise look enabled while
	/// silently matching nothing.
	/// </summary>
	internal static void ReportTableOnce()
	{
		if (_reportedTable)
		{
			return;
		}

		_reportedTable = true;
		var count = PinyinDictionary.Count;
		if (count == 0)
		{
			_log?.LogWarning(
				"[Pinyin] the embedded reading table is missing — pinyin search is inactive and every surface keeps the native substring rule.");
			return;
		}

		_log?.LogInformation("[Pinyin] search enabled — {Count} characters loaded.", count);
	}
}
