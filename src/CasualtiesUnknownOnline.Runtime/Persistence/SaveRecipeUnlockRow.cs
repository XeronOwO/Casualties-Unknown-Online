namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// One recipe's unlock state as the game stores it
/// (<c>Recipes.recipes[i].hasMadeBefore</c> / <c>.INT</c>). The index is written
/// explicitly, not implied by position: the game's own save reads the list
/// positionally, but a mod-updated build can inject recipes
/// (<c>GameAdapterRecipeContentProvider</c> appends to <c>Recipes.recipes</c>),
/// and a restore that wrote row <i>n</i> onto recipe <i>n</i> after such a change
/// would unlock the wrong thing. A row whose index no longer exists is refused by
/// name instead.
/// </summary>
public sealed class SaveRecipeUnlockRow
{
	/// <summary>The recipe's index in the game's <c>Recipes.recipes</c> table.</summary>
	public int Index { get; init; }

	/// <summary><c>Recipe.hasMadeBefore</c> — true once the player has crafted it.</summary>
	public bool MadeBefore { get; init; }

	/// <summary>
	/// <c>Recipe.INT</c> — the game's secondary visibility value
	/// (<c>RecipeUnlockApply</c> writes 0 to make an unlocked recipe draw its
	/// normal icon instead of the locked sprite).
	/// </summary>
	public int IntValue { get; init; }
}
