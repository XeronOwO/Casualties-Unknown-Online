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

	/// <summary>
	/// A recipe-unlock fact arrived (the host's own report or a relayed remote
	/// unlock). Write the static INT = 0. When the write is a NEW learn
	/// (previous INT was non-zero) also show the same "learned recipe" popup
	/// the game's native blueprint use shows (Item.cs:4285-4287) — the acting
	/// side already showed it natively, so the pre-write check suppresses the
	/// duplicate on that side.
	/// </summary>
	private void OnRecipeUnlockReceived(int recipeIndex)
	{
		if (Recipes.recipes == null || recipeIndex < 0 || recipeIndex >= Recipes.recipes.Count)
		{
			_log.LogWarning("[Crafting] recipe unlock index {Index} out of range — ignored.", recipeIndex);
			return;
		}

		var recipe = Recipes.recipes[recipeIndex];
		var newlyUnlocked = ShouldShowPopup(recipe.INT);
		recipe.INT = 0;

		if (newlyUnlocked)
		{
			ShowNewlyUnlockedPopup(recipeIndex, recipe);
		}

		_log.LogInformation("[Crafting] recipe {Index} unlocked ({Name}).", recipeIndex, recipe.fullName);
	}

	/// <summary>Only a transition INTO the learned state (INT != 0 → 0) needs the popup; an already-learned recipe must not re-alert on every duplicate relay.</summary>
	internal static bool ShouldShowPopup(int previousInt) => previousInt != 0;

	/// <summary>Pure text builder — same replacement the game performs for the native blueprint popup (Item.cs:4285-4287).</summary>
	internal static string BuildPopupText(string learnedRecipeTemplate, string itemName)
		=> learnedRecipeTemplate.Replace("r1", itemName);

	private void ShowNewlyUnlockedPopup(int recipeIndex, Recipe recipe)
	{
		if (PlayerCamera.main == null) // Unity object — ==
		{
			_log.LogWarning("[Crafting] recipe {Index} unlocked before PlayerCamera exists — popup skipped.", recipeIndex);
			return;
		}

		var text = BuildPopupText(Locale.GetOther("learnedrecipe"), Locale.GetItem(recipe.simpleName));
		PlayerCamera.main.DoAlert(text, false);
		_log.LogInformation("[Crafting] showed recipe-unlock popup ({Name}).", recipe.fullName);
	}
}
