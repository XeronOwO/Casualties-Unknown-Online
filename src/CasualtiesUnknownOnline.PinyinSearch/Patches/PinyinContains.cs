using System;
using CasualtiesUnknownOnline.PinyinSearch.Core.Search;

namespace CasualtiesUnknownOnline.PinyinSearch.Patches;

/// <summary>
/// The comparison the native recipe filter runs after
/// <see cref="PinyinSearchPatches"/> has rewritten its call site. The shape is
/// the BCL method's, so the IL replacement is a drop-in: inside the one-refresh
/// scope a name matches by literal substring or by pinyin, and every call
/// outside the scope keeps the native answer exactly.
/// </summary>
public static class PinyinContains
{
	/// <summary>
	/// The extended <c>string.Contains(string, StringComparison)</c>: true when
	/// the scope holds a query and the name matches it by literal or pinyin,
	/// otherwise the native comparison's own answer.
	/// </summary>
	public static bool Contains(string instance, string filter, StringComparison comparisonType)
	{
		if (RecipeSearchScope.IsFiltering
			&& !string.IsNullOrEmpty(filter)
			&& !string.IsNullOrEmpty(instance)
			&& NameSearchMatcher.Matches(instance, filter))
		{
			return true;
		}

		return instance.Contains(filter, comparisonType);
	}
}
