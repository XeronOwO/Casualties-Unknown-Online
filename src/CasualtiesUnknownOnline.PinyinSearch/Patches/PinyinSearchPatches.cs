using System;
using CasualtiesUnknownOnline.PinyinSearch.Core;
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
/// inside a compiler-generated lambda. Two shapes were measured dead on the live
/// client (batch 20261108): a hook on <c>Recipe.get_simpleName</c> can only
/// REMOVE rows, because the predicate still runs its own <c>Contains</c> on the
/// returned name; a transpiler on <c>RefreshRecipeList</c> finds no comparison
/// call, because the call lives in the lambda's own method. The extension
/// therefore lands on the comparison itself — the
/// <c>string.Contains(string, StringComparison)</c> overload — for the duration
/// of exactly one refresh, and answers the pinyin question before the native
/// comparison runs.
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

			// One line per distinct query names the exact string this build feeds the filter (an id, a
			// localized name, or something else), so a pinyin query that finds nothing can be diagnosed
			// from the log without another build.
			if (!string.Equals(_probedValue, value, StringComparison.Ordinal))
			{
				_probedValue = value;
				PinyinSearchGate.Log?.LogInfo($"[Pinyin] probe filter='{value}' instance='{__instance}'.");
			}

			if (NameSearchMatcher.Matches(__instance, value))
			{
				PinyinSearchGate.Log?.LogInfo($"[Pinyin] extended match: '{value}' matched '{__instance}'.");
				__result = true;
				return false;
			}

			return true;
		}

		private static string _probedValue = string.Empty;
	}
}
