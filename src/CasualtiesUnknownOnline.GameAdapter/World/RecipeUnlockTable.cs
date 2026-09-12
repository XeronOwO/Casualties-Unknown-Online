using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The game's own recipe unlock table (<c>Recipes.recipes</c>) as the save layer
/// needs it: read every entry's <c>hasMadeBefore</c> / <c>INT</c>, and write a
/// restored set back by INDEX.
///
/// Index, not position, is what a restore writes by. The native save reads its
/// list positionally (<c>SaveSystem.cs:442-447</c>), which is safe for the game
/// because the table it wrote is the table it reads; CUO's archive outlives a
/// mod update, and <c>GameAdapterRecipeContentProvider</c> APPENDS custom recipes
/// to <c>Recipes.recipes</c>, so writing row <i>n</i> onto recipe <i>n</i> after
/// such a change would unlock a different recipe than the one that was unlocked.
/// A row whose index no longer exists is refused by name — never written to a
/// neighbour.
/// </summary>
internal static class RecipeUnlockTable
{
	/// <summary>
	/// Every entry of the live recipe table, or null when the table is not built
	/// yet (a caller must treat null as "could not read", never as "nothing is
	/// unlocked" — an empty table would re-lock everything the player learned).
	/// </summary>
	internal static IReadOnlyList<SaveRecipeUnlockRow>? Capture()
	{
		var recipes = Recipes.recipes;
		if (recipes is null)
		{
			return null;
		}

		var rows = new List<SaveRecipeUnlockRow>(recipes.Count);
		for (var i = 0; i < recipes.Count; i++)
		{
			var recipe = recipes[i];
			if (recipe is null)
			{
				// A null slot is not a recipe whose unlock is unknown; it is a
				// table this build cannot describe. Refusing the read keeps the
				// cut from writing a table that would shift every later index.
				return null;
			}

			rows.Add(new SaveRecipeUnlockRow
			{
				Index = i,
				MadeBefore = recipe.hasMadeBefore,
				IntValue = recipe.INT,
			});
		}

		return rows;
	}

	/// <summary>
	/// Writes the restored set absolutely: every restored row's value is written
	/// onto the live recipe at that index. Rows whose index left the table are
	/// collected as refusals (the caller reports them); nothing else changes, and
	/// nothing is written to a neighbouring index.
	/// </summary>
	internal static RecipeUnlockApplyResult Apply(IReadOnlyList<SaveRecipeUnlockRow> rows)
	{
		var recipes = Recipes.recipes;
		if (recipes is null)
		{
			return new RecipeUnlockApplyResult(0, [.. Indices(rows)]);
		}

		var refused = new List<int>();
		var applied = 0;
		foreach (var row in rows)
		{
			if (row.Index < 0 || row.Index >= recipes.Count || recipes[row.Index] is null)
			{
				refused.Add(row.Index);
				continue;
			}

			var recipe = recipes[row.Index];
			recipe.hasMadeBefore = row.MadeBefore;
			recipe.INT = row.IntValue;
			applied++;
		}

		return new RecipeUnlockApplyResult(applied, refused);
	}

	private static IEnumerable<int> Indices(IReadOnlyList<SaveRecipeUnlockRow> rows)
	{
		foreach (var row in rows)
		{
			yield return row.Index;
		}
	}

	/// <summary>What one absolute write did: the rows the live table took, and the indices it has no recipe for.</summary>
	internal readonly record struct RecipeUnlockApplyResult(int Applied, IReadOnlyList<int> RefusedIndexes);
}
