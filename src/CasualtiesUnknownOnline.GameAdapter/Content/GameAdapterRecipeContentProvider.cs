using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Binds <see cref="ModRecipeDefinition"/> payloads from shared-content mods
/// into the vanilla recipe table. It waits for <c>Recipes.recipes</c> to be
/// initialized, builds plain game <c>Recipe</c> objects from the mod DTO, and
/// injects them exactly once per recipe-table generation.
/// </summary>
public sealed class GameAdapterRecipeContentProvider(
	ILogger<GameAdapterRecipeContentProvider> log) : IContentBindingProvider, ICuoService
{
	private readonly ILogger<GameAdapterRecipeContentProvider> _log = log;
	private readonly Dictionary<string, ModRecipeDefinition> _definitions = [];
	private readonly HashSet<string> _injectedKeys = [];
	private readonly HashSet<string> _failedKeys = [];
	private List<Recipe>? _lastRecipeList;

	/// <inheritdoc />
	public string Kind => ModContentKind.Recipe;

	/// <inheritdoc />
	public bool TryBind(ModContentRegistration registration)
	{
		if (!string.Equals(registration.Definition.Kind, Kind, StringComparison.Ordinal))
		{
			return false;
		}

		var definition = ModRecipeDefinition.FromPayload(registration.Definition.Data);
		if (definition is null)
		{
			_log.LogWarning(
				"[RecipeContent] {ModId}/{Id} payload is not a valid ModRecipeDefinition — refused.",
				registration.ModId, registration.Definition.Id);
			return false;
		}

		var id = registration.Definition.Id;
		if (string.IsNullOrWhiteSpace(id))
		{
			_log.LogWarning("[RecipeContent] {ModId} registered a recipe with an empty id — refused.", registration.ModId);
			return false;
		}

		if (string.IsNullOrWhiteSpace(definition.ResultItemId))
		{
			_log.LogWarning("[RecipeContent] {ModId}/{Id} has no result item id — refused.", registration.ModId, id);
			return false;
		}

		if (definition.Ingredients.Count == 0)
		{
			_log.LogWarning("[RecipeContent] {ModId}/{Id} has no ingredients — refused.", registration.ModId, id);
			return false;
		}

		if (_definitions.ContainsKey(id))
		{
			_log.LogWarning(
				"[RecipeContent] {ModId}/{Id} is already registered by another recipe-content provider/definition — refused.",
				registration.ModId, id);
			return false;
		}

		_definitions.Add(id, definition);
		_log.LogInformation(
			"[RecipeContent] accepted {ModId}/{Id} (schema {SchemaVersion}); injection waits for the vanilla recipe table.",
			registration.ModId, id, registration.Definition.SchemaVersion);
		return true;
	}

	public void Initialize()
	{
	}

	public void Start()
	{
	}

	public void Update()
	{
		if (Recipes.recipes is null)
		{
			_lastRecipeList = null;
			return;
		}

		if (!ReferenceEquals(_lastRecipeList, Recipes.recipes))
		{
			_lastRecipeList = Recipes.recipes;
			_injectedKeys.Clear(); // a new game/layer rebuilt the table; re-inject every accepted definition
			_failedKeys.Clear();   // failed reference checks are retried against the new world's tables
		}

		foreach (var pair in _definitions.ToArray())
		{
			if (_injectedKeys.Contains(pair.Key) || _failedKeys.Contains(pair.Key))
			{
				continue;
			}

			var recipe = BuildRecipe(pair.Key, pair.Value);
			if (recipe is null)
			{
				_failedKeys.Add(pair.Key);
				_log.LogWarning("[RecipeContent] {Id} could not be built — skipped.", pair.Key);
				continue;
			}

			var key = BuildRecipeKey(recipe);
			if (_injectedKeys.Contains(key) || Recipes.recipes.Any(existing => BuildRecipeKey(existing) == key))
			{
				_injectedKeys.Add(pair.Key);
				_injectedKeys.Add(key);
				_log.LogDebug("[RecipeContent] {Id} is already present in the vanilla recipe table; no duplicate injected.", pair.Key);
				continue;
			}

			recipe.index = Recipes.recipes.Count;
			Recipes.recipes.Add(recipe);
			_injectedKeys.Add(pair.Key);
			_injectedKeys.Add(key);
			_log.LogInformation(
				"[RecipeContent] injected {Id} (result {Result}, {IngredientCount} ingredients) into Recipes.recipes.",
				pair.Key, pair.Value.ResultItemId, pair.Value.Ingredients.Count);
		}
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
	}

	private Recipe? BuildRecipe(string id, ModRecipeDefinition definition)
	{
		if (!TryParseCategory(definition.Category, out var category))
		{
			_log.LogWarning("[RecipeContent] {Id} has unknown category {Category} — skipped.", id, definition.Category);
			return null;
		}

		if (!IsKnownReference(definition.ResultItemId, definition.ResultIsLiquid))
		{
			_log.LogWarning(
				"[RecipeContent] {Id} references unknown {Kind} '{Result}' — skipped.",
				id, definition.ResultIsLiquid ? "liquid" : "item", definition.ResultItemId);
			return null;
		}

		var recipe = new Recipe
		{
			result = new RecipeResult
			{
				id = definition.ResultItemId,
				isLiquid = definition.ResultIsLiquid,
				amount = definition.ResultAmount <= 0 ? 1 : definition.ResultAmount,
				resultCondition = definition.ResultCondition,
				dontDrainResultLiquid = definition.DontDrainResultLiquid
			},
			INT = definition.Intelligence,
			category = category,
			isRepair = definition.IsRepair,
			items = []
		};

		foreach (var ingredient in definition.Ingredients)
		{
			var specific = !string.IsNullOrWhiteSpace(ingredient.ItemId);
			if (!specific && string.IsNullOrWhiteSpace(ingredient.Quality))
			{
				_log.LogWarning(
					"[RecipeContent] {Id} ingredient requires either ItemId or Quality — skipped recipe.",
					id);
				return null;
			}

			if (specific && !IsKnownReference(ingredient.ItemId!, ingredient.IsLiquid))
			{
				_log.LogWarning(
					"[RecipeContent] {Id} references unknown ingredient {Kind} '{Item}' — skipped recipe.",
					id, ingredient.IsLiquid ? "liquid" : "item", ingredient.ItemId);
				return null;
			}

			recipe.items.Add(new RecipeItem(ingredient.MinimumCondition < 0f ? 0f : ingredient.MinimumCondition)
			{
				specific = specific,
				specificId = ingredient.ItemId ?? string.Empty,
				isLiquid = ingredient.IsLiquid,
				quality = specific || string.IsNullOrWhiteSpace(ingredient.Quality)
					? null
					: new CraftingQuality(ingredient.Quality, ingredient.QualityAmount <= 0f ? 1f : ingredient.QualityAmount),
				destroyItem = ingredient.DestroyItem,
				ignoredId = recipe.isRepair ? string.Empty : definition.ResultItemId
			});
		}

		return recipe;
	}

	/// <summary>
	/// Returns true when the referenced item/liquid is known, or when the game
	/// table that would answer is not ready yet (deferred validation).
	/// </summary>
	private bool IsKnownReference(string id, bool isLiquid)
	{
		if (isLiquid)
		{
			if (Liquids.Registry is null)
			{
				return true;
			}

			return Liquids.Registry.ContainsKey(id);
		}

		if (Item.GlobalItems is null)
		{
			return true;
		}

		return Item.GlobalItems.ContainsKey(id) || Resources.Load<GameObject>(id) != null; // Unity object — ==
	}

	private static string BuildRecipeKey(Recipe recipe)
	{
		var ingredients = string.Join("|", recipe.items.Select(item =>
			$"{item.specificId}|{item.isLiquid}|{item.minimumCondition}|{item.destroyItem}|{item.quality?.id}|{item.quality?.amount}"));
		return $"{recipe.result.id}|{recipe.result.amount}|{recipe.result.resultCondition}|{recipe.result.isLiquid}|{ingredients}";
	}

	private static bool TryParseCategory(string category, out Recipes.RecipeCategory value)
	{
		switch (category?.Trim().ToLowerInvariant())
		{
			case ModRecipeCategory.Materials:
				value = Recipes.RecipeCategory.Materials;
				return true;
			case ModRecipeCategory.Tools:
				value = Recipes.RecipeCategory.Tools;
				return true;
			case ModRecipeCategory.Medicine:
				value = Recipes.RecipeCategory.Medicine;
				return true;
			case ModRecipeCategory.Utilities:
				value = Recipes.RecipeCategory.Utilities;
				return true;
			case ModRecipeCategory.Food:
				value = Recipes.RecipeCategory.Food;
				return true;
			default:
				value = default;
				return false;
		}
	}
}
