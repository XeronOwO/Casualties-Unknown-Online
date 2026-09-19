using System.Collections.Generic;
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
///
/// Two shapes of the same fact arrive here: a single LIVE unlock (the host's own
/// report or a relayed remote unlock — the alert is shown to a side that had not
/// learned it yet) and the absolute SET (the host's world-entry / 60 s backfill,
/// sync-coverage audit I6 — no alert: the receiver is catching up on unlocks it
/// never performed, and one alert per recipe would fire the whole run's set at a
/// late joiner). Both write the same idempotent static value.
/// </summary>
internal sealed class RecipeUnlockApply(ICraftControl craft, ILogger<RecipeUnlockApply> log)
{
	private readonly ICraftControl _craft = craft;
	private readonly ILogger<RecipeUnlockApply> _log = log;

	internal void BindToSession()
	{
		_craft.RecipeUnlockReceived += OnRecipeUnlockReceived;
		_craft.RecipeUnlockSetReceived += OnRecipeUnlockSetReceived;
	}

	internal void Unbind()
	{
		_craft.RecipeUnlockReceived -= OnRecipeUnlockReceived;
		_craft.RecipeUnlockSetReceived -= OnRecipeUnlockSetReceived;
	}

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

		RefreshCraftingList();
		_log.LogInformation("[Crafting] recipe {Index} unlocked ({Name}).", recipeIndex, recipe.fullName);
	}

	/// <summary>
	/// An absolute set arrived: the host's authoritative unlocked set (world entry
	/// / the 60 s repair). Every index is written INT = 0 with NO alert — the
	/// receiver performed no unlock — and an index this table does not have is
	/// named in the log (the host's set can outlive content a mod update removed,
	/// and the host's own merge refuses the same row on its side).
	/// </summary>
	private void OnRecipeUnlockSetReceived(IReadOnlyList<int> recipeIndexes)
	{
		if (Recipes.recipes == null)
		{
			_log.LogWarning("[Crafting] {Count} unlocked recipe(s) arrived before the recipe table exists — the set is ignored.", recipeIndexes.Count);
			return;
		}

		var applied = 0;
		var refused = 0;
		foreach (var recipeIndex in recipeIndexes)
		{
			if (recipeIndex < 0 || recipeIndex >= Recipes.recipes.Count || Recipes.recipes[recipeIndex] is null)
			{
				_log.LogWarning("[Crafting] recipe unlock index {Index} out of range — ignored.", recipeIndex);
				refused++;
				continue;
			}

			Recipes.recipes[recipeIndex].INT = 0;
			applied++;
		}

		RefreshCraftingList();
		_log.LogInformation("[Crafting] applied the recipe-unlock set: {Applied} recipe(s), {Refused} refused.", applied, refused);
	}

	/// <summary>
	/// The game refreshes its crafting list in the same native branch that writes
	/// an unlock (<c>Item.cs:4288-4296</c>: the alert, then <c>OpenCraftScreen</c>
	/// or <c>RefreshRecipeList</c>), so a remote unlock — live or backfilled — must
	/// refresh it here too: otherwise the recipe is unlocked in the table but still
	/// missing from the list a player has open. Silent while the panel is closed
	/// (the list is rebuilt when it opens) and before the local player exists.
	/// </summary>
	private void RefreshCraftingList()
	{
		if (PlayerCamera.main == null) // Unity object — ==
		{
			return;
		}

		if (PlayerCamera.main.craftingPanel != null && PlayerCamera.main.craftingPanel.activeSelf) // Unity objects — ==
		{
			PlayerCamera.main.RefreshRecipeList();
		}
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
