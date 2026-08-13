using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The recipe-unlock apply shell (切离方法论: the game-side touch lives here,
/// the wire/relay judgment lives in CraftSyncService): every side — its own
/// blueprint use and the relayed reports alike — sets
/// Recipes.recipes[idx].INT = 0, which makes the recipe permanently visible
/// (Recipe.visible, Recipe.cs:98-104). The static recipe table is per-process,
/// so without this the unlock existed only on the user's side.
/// </summary>
internal sealed class RecipeUnlockApply(ICraftControl craft, ILogger<RecipeUnlockApply> log)
{
	private readonly ICraftControl _craft = craft;
	private readonly ILogger<RecipeUnlockApply> _log = log;

	internal void BindToSession() => _craft.RecipeUnlockReceived += OnRecipeUnlockReceived;

	internal void Unbind() => _craft.RecipeUnlockReceived -= OnRecipeUnlockReceived;

	private void OnRecipeUnlockReceived(int recipeIndex)
	{
		if (Recipes.recipes == null || recipeIndex < 0 || recipeIndex >= Recipes.recipes.Count)
		{
			_log.LogWarning("[Crafting] recipe unlock index {Index} out of range — ignored.", recipeIndex);
			return;
		}

		Recipes.recipes[recipeIndex].INT = 0;
		_log.LogInformation("[Crafting] recipe {Index} unlocked ({Name}).", recipeIndex, Recipes.recipes[recipeIndex].fullName);
	}
}
