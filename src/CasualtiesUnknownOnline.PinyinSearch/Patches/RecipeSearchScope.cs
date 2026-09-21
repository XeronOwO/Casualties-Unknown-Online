using System.Reflection;
using CasualtiesUnknownOnline.PinyinSearch.Core;
using CasualtiesUnknownOnline.PinyinSearch.Core.Search;
using HarmonyLib;

namespace CasualtiesUnknownOnline.PinyinSearch.Patches;

/// <summary>
/// The query the native recipe list is being rebuilt with. The native predicate
/// is a compiler-generated lambda inside <c>PlayerCamera.RefreshRecipeList</c>,
/// so a matcher cannot be spliced into it; instead the native predicate keeps
/// its own comparison and reads names through <c>Recipe.simpleName</c>, which
/// this scope borrows for the duration of exactly one refresh (see
/// <see cref="PinyinSearchPatches"/>). Ordering, the category filter, the row
/// objects, the scroll position and the selection index all stay native.
///
/// The scope is a single-threaded UI-frame ambient: the Unity main thread runs
/// the refresh and the getter calls it makes, and the patch's postfix and
/// finalizer both close it.
/// </summary>
internal static class RecipeSearchScope
{
	/// <summary>Consecutive searches with an active query that read no name at all before the read path is reported as possibly gone.</summary>
	private const int SilentRefreshLimit = 3;

	/// <summary>The camera's private "recipes containing this item" filter — while it is set the native refresh ignores the text filter entirely.</summary>
	private static readonly FieldInfo? ItemFilterField =
		AccessTools.Field(typeof(PlayerCamera), "recipeItemFilter");

	private static bool _reportedMissingItemFilter;
	private static string? _query;
	private static int _namesRead;
	private static int _silentRefreshes;
	private static bool _reportedSilentReadPath;

	/// <summary>The query borrowed for this refresh, or null when the native behavior must stand.</summary>
	internal static string? Query => _query;

	internal static void Begin(PlayerCamera camera)
	{
		_query = null;
		_namesRead = 0;

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
		PinyinSearchGate.Log?.LogDebug($"[Pinyin] recipe list filtered by '{filter}'.");
	}

	/// <summary>Counts one name the native refresh asked for; see <see cref="ReportSilentReadPath"/>.</summary>
	internal static void NoteNameRead() => _namesRead++;

	internal static void End()
	{
		if (_query is not null)
		{
			if (_namesRead == 0)
			{
				ReportSilentReadPath();
			}
			else
			{
				_silentRefreshes = 0;
			}
		}

		_query = null;
	}

	/// <summary>
	/// The read path this hook depends on, made observable — with the exact
	/// scope of what it proves. The native search reads <c>Recipe.simpleName</c>
	/// for the recipes it filters and again for every row it builds, so a
	/// refresh with an active query that consulted no name at all means the game
	/// stopped reading that getter: pinyin search cannot filter, and no patch
	/// contract can see it, because the read lives in a compiler-generated
	/// lambda. The scope reports that shape itself, once, after a few
	/// consecutive silent refreshes; an empty recipe list produces the same
	/// count, which is why the wording states a possibility rather than a
	/// verdict. What it does NOT prove: a predicate that switched to a different
	/// name while the row-building loop kept reading <c>simpleName</c> would
	/// keep this counter fed — that residue is recorded as a limit, not covered
	/// here.
	/// </summary>
	private static void ReportSilentReadPath()
	{
		_silentRefreshes++;
		if (_silentRefreshes < SilentRefreshLimit || _reportedSilentReadPath)
		{
			return;
		}

		_reportedSilentReadPath = true;
		PinyinSearchGate.Log?.LogWarning(
			$"[Pinyin] {_silentRefreshes} consecutive refreshes with an active query consulted no recipe name — "
			+ "either the recipe list was empty or the native filter no longer reads Recipe.simpleName, in which "
			+ "case pinyin search is inactive. Reported once.");
	}

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
