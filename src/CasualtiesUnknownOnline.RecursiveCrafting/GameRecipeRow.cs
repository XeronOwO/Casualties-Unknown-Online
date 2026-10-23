using CasualtiesUnknownOnline.RecursiveCrafting.Core.Crafting;

namespace CasualtiesUnknownOnline.RecursiveCrafting;

/// <summary>
/// One row of the game's recipe table as the model can see it: a supported recipe, or the reason it was skipped.
/// A skipped row keeps its result id so the command can still say "this item's recipe is not modelled yet"
/// instead of "no recipe produces it" — the player asked about the item, not about our coverage.
/// </summary>
internal sealed class GameRecipeRow(CraftRecipe? recipe, string? skipReason, string resultItemId)
{
	/// <summary>The model recipe, or null when the row could not be described honestly.</summary>
	internal CraftRecipe? Recipe { get; } = recipe;

	/// <summary>Why the row was skipped (only meaningful when <see cref="Recipe"/> is null).</summary>
	internal string? SkipReason { get; } = skipReason;

	/// <summary>The item id the row produces, so a skipped row can still be named.</summary>
	internal string ResultItemId { get; } = resultItemId;
}
