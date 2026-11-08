using System;
using CasualtiesUnknownOnline.PinyinSearch.Core.Search;
using HarmonyLib;

namespace CasualtiesUnknownOnline.PinyinSearch.Patches;

/// <summary>
/// Pinyin search on the game's own crafting search box. The player keeps using
/// the native field, the native list, the native "clear" button and the native
/// category buttons; only the matching rule is extended, and only while the
/// mod's switch is on.
///
/// The native filter is <c>simpleName.Contains(recipeFilter, OrdinalIgnoreCase)</c>
/// evaluated inside a compiler-generated lambda. A name hook cannot ADD a match
/// — the predicate still runs its own <c>Contains</c> on whatever name the hook
/// returns, so a pinyin-only hit would be dropped again. The extension
/// therefore lands on the COMPARISON: while one refresh is being filtered, the
/// <c>string.Contains(string, StringComparison)</c> overload answers the pinyin
/// question first and falls back to the native comparison otherwise. Outside
/// the scope the overload is untouched, so every other <c>Contains</c> in the
/// game keeps its exact behavior.
/// </summary>
internal static class PinyinSearchPatches
{
	/// <summary>
	/// Opens and closes the query scope around exactly one native refresh, so
	/// the comparison hook below is inert everywhere else.
	/// </summary>
	[HarmonyPatch(typeof(PlayerCamera), "RefreshRecipeList")]
	internal static class RefreshRecipeListPatch
	{
		private static void Prefix(PlayerCamera __instance) => RecipeSearchScope.Begin(__instance);

		private static void Postfix() => RecipeSearchScope.End();

		/// <summary>Runs even when the native body throws, so a failed refresh cannot leave the borrowed scope open into the next frame.</summary>
		private static void Finalizer() => RecipeSearchScope.End();
	}

	/// <summary>
	/// The extended comparison: inside the scope, a name that matches by literal
	/// substring or by pinyin answers true; anything else (and every call
	/// outside the scope) runs the native comparison unchanged.
	/// </summary>
	[HarmonyPatch(typeof(string), nameof(string.Contains), [typeof(string), typeof(StringComparison)])]
	internal static class StringContainsPatch
	{
		private static bool Prefix(string __instance, string value, StringComparison comparisonType, ref bool __result)
		{
			if (!RecipeSearchScope.IsFiltering || string.IsNullOrEmpty(value) || string.IsNullOrEmpty(__instance))
			{
				return true;
			}

			if (NameSearchMatcher.Matches(__instance, value))
			{
				__result = true;
				return false;
			}

			return true;
		}
	}
}
