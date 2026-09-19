using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

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
	/// The live table's UNLOCKED indices — every recipe whose <c>INT</c> is 0,
	/// which is both what the game's own blueprint use writes
	/// (<c>Item.cs:4284</c>) and the state that makes a recipe draw no INT
	/// requirement at all (<c>Recipe.visible</c>, <c>Recipe.cs:98-104</c>). A few
	/// recipes are set up at 0 by the game itself (<c>Recipes.cs</c>), so the set
	/// means "recipes that need no skill" — exactly the fact the crafting list
	/// shows; sending such an index writes to a peer the 0 its own table already
	/// holds.
	///
	/// Null = the table cannot be read (see <see cref="Capture"/>), never an empty
	/// set standing in for it: the caller's empty set means "nothing is unlocked",
	/// which asks for no write at all.
	/// </summary>
	internal static IReadOnlyList<int>? CaptureUnlockedIndexes()
	{
		var rows = Capture();
		if (rows is null)
		{
			return null;
		}

		var indices = new List<int>();
		foreach (var row in rows)
		{
			if (row.IntValue == 0)
			{
				indices.Add(row.Index);
			}
		}

		return indices;
	}

	/// <summary>
	/// Writes the restored set absolutely: every restored row's value is written
	/// onto the live recipe at that index. Rows whose index left the table are
	/// collected as refusals (the caller reports them); nothing else changes, and
	/// nothing is written to a neighbouring index.
	///
	/// The rows run through the Runtime's per-row containment
	/// (<see cref="ContainedRowLoop.RunContained"/>), so a row reaching an engine call this
	/// copy cannot serve costs ITSELF — named at error level with its index — instead of every
	/// row behind it, and it is refused as its OWN class
	/// (<see cref="RecipeUnlockApplyResult.RefusedByThrow"/>) rather than as a missing recipe:
	/// the index EXISTS in the table, the write threw, and reporting that as "this table has no
	/// recipe here" would name the wrong reason.
	/// </summary>
	internal static RecipeUnlockApplyResult Apply(IReadOnlyList<SaveRecipeUnlockRow> rows, ILogger log)
	{
		var recipes = Recipes.recipes;
		if (recipes is null)
		{
			return new RecipeUnlockApplyResult(0, [.. Indices(rows)], 0);
		}

		var refused = new List<int>();
		var applied = 0;
		var thrown = ContainedRowLoop.RunContained(
			rows,
			row =>
			{
				if (row.Index < 0 || row.Index >= recipes.Count || recipes[row.Index] is null)
				{
					refused.Add(row.Index);
					return;
				}

				var recipe = recipes[row.Index];
				recipe.hasMadeBefore = row.MadeBefore;
				recipe.INT = row.IntValue;
				applied++;
			},
			row => $"index {row.Index}",
			log,
			"restored recipe unlock");

		return new RecipeUnlockApplyResult(applied, refused, thrown);
	}

	private static IEnumerable<int> Indices(IReadOnlyList<SaveRecipeUnlockRow> rows)
	{
		foreach (var row in rows)
		{
			yield return row.Index;
		}
	}

	/// <summary>
	/// What one absolute write did: the rows the live table took, the indices it has no recipe
	/// for, and how many rows reached an engine call the local copy cannot serve. The throwing
	/// rows are their OWN class rather than entries in <see cref="RefusedIndexes"/>: their index
	/// EXISTS in the table and the write threw, so reporting them as missing recipes would name
	/// the wrong reason. The row itself is named by the containment's error line, whose identity
	/// is its index — the only key a recipe row has.
	/// </summary>
	internal readonly record struct RecipeUnlockApplyResult(int Applied, IReadOnlyList<int> RefusedIndexes, int RefusedByThrow);
}
