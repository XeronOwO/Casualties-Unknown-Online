using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.RecursiveCrafting.Core.Crafting;

namespace CasualtiesUnknownOnline.RecursiveCrafting;

/// <summary>
/// The game-side reader: the game's own recipe table (<c>Recipes.recipes</c>) and the local body's items
/// (<c>Body.GetAllItemsThorough</c>) into the Core's model. It knows the two ingredient shapes the read of
/// <c>RecipeItem</c> documents — a specific id and a crafting quality — and it REFUSES to describe what it
/// cannot: a liquid line, or a line with neither an id nor a quality, skips that recipe with a named reason
/// instead of being approximated into a chain that would lie.
/// </summary>
internal static class GameCraftingTable
{
	/// <summary>A recipe with a liquid ingredient line: the model does not carry liquids yet.</summary>
	internal const string LiquidSkipReason = "a liquid ingredient line (not modelled yet)";

	/// <summary>A recipe whose line has neither an item id nor a crafting quality.</summary>
	internal const string EmptyLineSkipReason = "an ingredient line with no item id and no crafting quality";

	/// <summary>A row the table could not read at all (no result id).</summary>
	internal const string NoResultSkipReason = "a row with no result item";

	/// <summary>Read the game's table, in the table's own order.</summary>
	internal static IReadOnlyList<GameRecipeRow> ReadRecipes()
	{
		var rows = new List<GameRecipeRow>();
		var source = Recipes.recipes;
		if (source == null)
		{
			return rows;
		}

		var resultCounts = source
			.Where(recipe => recipe?.result != null && !string.IsNullOrEmpty(recipe.result.id))
			.GroupBy(recipe => recipe.result.id, StringComparer.Ordinal)
			.ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
		var seen = new Dictionary<string, int>(StringComparer.Ordinal);

		foreach (var recipe in source)
		{
			var result = recipe?.result;
			if (recipe == null || result == null || string.IsNullOrEmpty(result.id))
			{
				rows.Add(new GameRecipeRow(null, NoResultSkipReason, string.Empty));
				continue;
			}

			var resultId = result.id;
			var index = seen.TryGetValue(resultId, out var count) ? count + 1 : 1;
			seen[resultId] = index;
			var id = resultCounts[resultId] > 1 ? $"{resultId}#{index}" : resultId;

			var lines = new List<CraftIngredient>();
			var skip = ReadLines(recipe, lines);
			rows.Add(skip is null
				? new GameRecipeRow(new CraftRecipe(id, resultId, lines, Math.Max(1, result.amount)), null, resultId)
				: new GameRecipeRow(null, skip, resultId));
		}

		return rows;
	}

	/// <summary>
	/// The recipe's own lines as model lines; returns the skip reason when one cannot be described. The condition
	/// a line demands is the game's OWN test — <c>item.condition &gt;= line.minimumCondition</c> — not the
	/// quality amount: the amount is what <c>UseItem</c> subtracts afterwards, and using it as a floor would
	/// refuse a partly spent item the game itself accepts. The mod's "two into one" rule then works on top: a
	/// line whose minimum condition exceeds one held item may be satisfied by two whose conditions sum to it.
	/// </summary>
	private static string? ReadLines(Recipe recipe, List<CraftIngredient> lines)
	{
		if (recipe.items == null || recipe.items.Count == 0)
		{
			return EmptyLineSkipReason;
		}

		foreach (var line in recipe.items)
		{
			if (line == null)
			{
				return EmptyLineSkipReason;
			}

			if (line.isLiquid)
			{
				return LiquidSkipReason;
			}

			if (line.specific)
			{
				if (string.IsNullOrEmpty(line.specificId))
				{
					return EmptyLineSkipReason;
				}

				lines.Add(new CraftIngredient(line.specificId, amount: 1, quality: null, totalCondition: Math.Max(0f, line.minimumCondition)));
				continue;
			}

			if (line.quality == null || string.IsNullOrEmpty(line.quality.id))
			{
				return EmptyLineSkipReason;
			}

			lines.Add(CraftIngredient.OfQuality(line.quality.id, Math.Max(0f, line.minimumCondition), amount: 1));
		}

		return null;
	}

	/// <summary>
	/// The local body's items as held stacks. Favourited items are left OUT because the game's own recipe
	/// matching refuses to consume them (<c>RecipeItem.GetMatchingItem</c> skips <c>item.favourited</c>): a
	/// chain that assumed them would tell the player to craft with an item the game will not take.
	/// </summary>
	internal static List<CraftStack> ReadInventory()
	{
		var stacks = new List<CraftStack>();
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null)
		{
			return stacks;
		}

		foreach (var item in body.GetAllItemsThorough())
		{
			if (item == null || item.favourited)
			{
				continue;
			}

			var tags = item.Stats?.qualities == null
				? []
				: item.Stats.qualities
					.Where(quality => quality != null && !string.IsNullOrEmpty(quality.id))
					.Select(quality => quality.id)
					.ToArray();
			stacks.Add(new CraftStack(item.id, amount: 1, condition: item.condition, tags));
		}

		return stacks;
	}
}
