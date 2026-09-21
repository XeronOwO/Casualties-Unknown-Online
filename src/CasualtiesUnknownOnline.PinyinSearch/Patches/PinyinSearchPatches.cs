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
/// evaluated inside a compiler-generated lambda, so the extension lands on the
/// name the predicate reads rather than on a re-implementation of the list:
/// inside one refresh, a recipe whose name does not match answers with an empty
/// string, which no non-empty query is a substring of. Nothing is duplicated
/// and nothing native is destroyed or re-laid out.
/// </summary>
internal static class PinyinSearchPatches
{
	/// <summary>
	/// Opens and closes the borrowed-name scope around exactly one native
	/// refresh, so the getter hook below is inert everywhere else.
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
	/// The extended predicate: the real name when the query matches literally or
	/// by pinyin, an empty string when it does not. The getter is untouched while
	/// no query is in scope — including the row tooltips the refresh itself
	/// builds, which only ever see matched (hence unchanged) names.
	/// </summary>
	[HarmonyPatch(typeof(Recipe), "get_simpleName")]
	internal static class RecipeSimpleNamePatch
	{
		private static void Postfix(ref string __result)
		{
			if (RecipeSearchScope.Query is not { } query)
			{
				return;
			}

			// The refresh consulted a name: proof (per search) that the native
			// filter still reads through this getter.
			RecipeSearchScope.NoteNameRead();

			if (!NameSearchMatcher.Matches(__result, query))
			{
				__result = string.Empty;
			}
		}
	}
}
