using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.PinyinSearch.Core.Search;

namespace CasualtiesUnknownOnline.PinyinSearch.Core;

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
/// The mod's own switch is read live, and disabled means no match and no reading
/// table load.
/// </summary>
internal sealed class PinyinSearchStage : IResourceLocationMatchStage
{
	/// <inheritdoc />
	public bool Matches(ResourceLocationEntry entry, string prefix)
	{
		if (!PinyinSearchGate.Enabled || string.IsNullOrEmpty(prefix))
		{
			return false;
		}

		PinyinSearchGate.ReportTableOnce();

		if (!PinyinMatcher.Contains(entry.DisplayName, prefix))
		{
			return false;
		}

		PinyinSearchGate.Log?.LogDebug(
			$"[Pinyin] console completion '{prefix}' matched {entry.Id} by display name.");
		return true;
	}
}
