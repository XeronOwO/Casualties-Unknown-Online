using System.Collections.Generic;

namespace CasualtiesUnknownOnline.RecursiveCrafting.Core.Crafting;

/// <summary>
/// One craftable recipe in the mod's own model: what it produces and what it consumes. The resolver never
/// mutates a recipe; a chain is a list of these, with the target last.
/// </summary>
public sealed class CraftRecipe(string id, string resultItemId, IReadOnlyList<CraftIngredient> ingredients, int resultAmount = 1)
{
	/// <summary>The recipe's own id (the game's row identity).</summary>
	public string Id { get; } = id;

	/// <summary>The item id one completed craft produces.</summary>
	public string ResultItemId { get; } = resultItemId;

	/// <summary>How many units one completed craft produces.</summary>
	public int ResultAmount { get; } = resultAmount;

	/// <summary>What one completed craft consumes.</summary>
	public IReadOnlyList<CraftIngredient> Ingredients { get; } = ingredients;
}
