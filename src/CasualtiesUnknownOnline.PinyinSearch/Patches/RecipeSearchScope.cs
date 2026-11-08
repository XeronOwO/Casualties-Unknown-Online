using System.Reflection;
using CasualtiesUnknownOnline.PinyinSearch.Core;
using CasualtiesUnknownOnline.PinyinSearch.Core.Search;
using HarmonyLib;

namespace CasualtiesUnknownOnline.PinyinSearch.Patches;

/// <summary>
/// The query one native recipe refresh is being filtered with. The native
/// predicate is a compiler-generated lambda inside
/// <c>PlayerCamera.RefreshRecipeList</c>, so the extension cannot be spliced
/// into it directly; instead the scope borrows the
/// <c>string.Contains(string, StringComparison)</c> overload for the duration
/// of exactly one refresh (see <see cref="PinyinSearchPatches"/>). Ordering,
/// the category filter, the row objects, the scroll position and the selection
/// index all stay native.
///
/// The scope is a single-threaded UI-frame ambient: the Unity main thread runs
/// the refresh and the comparisons it makes, and the patch's postfix and
/// finalizer both close it.
/// </summary>
internal static class RecipeSearchScope
{
	/// <summary>The camera's private "recipes containing this item" filter — while it is set the native refresh ignores the text filter entirely.</summary>
	private static readonly FieldInfo? ItemFilterField =
		AccessTools.Field(typeof(PlayerCamera), "recipeItemFilter");

	private static bool _reportedMissingItemFilter;
	private static string? _query;

	/// <summary>The query borrowed for this refresh, or null when the native behavior must stand.</summary>
	internal static string? Query => _query;

	/// <summary>Whether a refresh is currently filtering through this scope.</summary>
	internal static bool IsFiltering => _query is not null;

	internal static void Begin(PlayerCamera camera)
	{
		_query = null;

		// The item-filter probe reads a private field and reports a missing one
		// once, so it runs only when a search could actually be extended — a
		// player who never uses pinyin search must not see that warning.
		var enabled = PinyinSearchGate.Enabled;
		var filter = camera.recipeFilter;
		var itemFilterActive = enabled && !string.IsNullOrEmpty(filter) && HasItemFilter(camera);
		_query = RecipeSearchDecision.QueryFor(enabled, filter, itemFilterActive);
		if (_query is null)
		{
			return;
		}

		PinyinSearchGate.ReportTableOnce();
		PinyinSearchGate.Log?.LogInfo($"[Pinyin] recipe list filtered by '{filter}'.");
	}

	internal static void End() => _query = null;

	private static bool HasItemFilter(PlayerCamera camera)
	{
		var field = ItemFilterField;
		if (field is null)
		{
			// The field is gone (game update): the guard cannot be evaluated, and
			// filtering would be the unsafe guess — an item-filter view would lose
			// rows. Keep the native behavior and report the reason once.
			if (!_reportedMissingItemFilter)
			{
				_reportedMissingItemFilter = true;
				PinyinSearchGate.Log?.LogWarning(
					"[Pinyin] PlayerCamera.recipeItemFilter is missing — pinyin search keeps the native filter "
					+ "while an item filter may be active.");
			}

			return true;
		}

		return field.GetValue(camera) is Item item && item != null; // Unity object — ==
	}
}
