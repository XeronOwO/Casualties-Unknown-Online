using System;

namespace CasualtiesUnknownOnline.PinyinSearch.Core.Search;

/// <summary>
/// The search predicate every pinyin-enabled native surface shares: the
/// existing ordinal-ignore-case substring rule OR a pinyin reading match. Both
/// halves are needed — the substring rule keeps an English name, a digit or a
/// half-typed query behaving exactly as it did before, and the pinyin half is
/// purely additive.
/// </summary>
internal static class NameSearchMatcher
{
	/// <summary>
	/// True when <paramref name="query"/> matches <paramref name="name"/> either
	/// literally (the native rule) or by pinyin. An empty query matches
	/// everything, which is the native search box's own contract.
	/// </summary>
	public static bool Matches(string name, string query)
	{
		if (string.IsNullOrEmpty(query))
		{
			return true;
		}

		if (string.IsNullOrEmpty(name))
		{
			return false;
		}

		return name.Contains(query, StringComparison.OrdinalIgnoreCase)
			|| PinyinMatcher.Contains(name, query);
	}
}
