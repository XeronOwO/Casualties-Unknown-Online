using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.Runtime.Session.Content;

/// <summary>
/// The console's pinyin completion stage: a Chinese display name also completes
/// by pinyin — full pinyin, initials, mixed Chinese/pinyin input — through the
/// same matcher core the native crafting search box uses, so both surfaces agree
/// on what "matches" means. It is deliberately additive: the catalog consults it
/// only for entries the built-in ranks did not match, so it can never displace
/// an exact id, an id prefix, a bare-path or a display-name match.
///
/// Reusing that core also brings its literal half: the matcher compares plain
/// characters anywhere in the name, so with the switch on an ASCII display name
/// matches mid-string ("ooden" → "Wooden Sword") — more than the built-in
/// display-name PREFIX rank offers, and the price of both surfaces sharing one
/// predicate. The widening is switch-gated and purely additive.
///
/// <c>Search.PinyinSearch</c> is read live from the options monitor (the same
/// singleton the Game Adapter's patch gate reads), so a config edit applies to
/// the next keystroke without a restart. Disabled means no match and no reading
/// table load.
/// </summary>
public sealed class PinyinResourceLocationMatchStage(
	IOptionsMonitor<PinyinSearchOptions> options,
	ILogger<PinyinResourceLocationMatchStage> log) : IResourceLocationMatchStage
{
	/// <inheritdoc />
	public bool Matches(ResourceLocationEntry entry, string prefix)
	{
		if (!options.CurrentValue.Enabled || string.IsNullOrEmpty(prefix))
		{
			return false;
		}

		PinyinTableReport.ReportOnce(log);

		if (!PinyinMatcher.Contains(entry.DisplayName, prefix))
		{
			return false;
		}

		log.LogDebug("[Pinyin] console completion '{Prefix}' matched {Id} by display name.", prefix, entry.Id);
		return true;
	}
}
