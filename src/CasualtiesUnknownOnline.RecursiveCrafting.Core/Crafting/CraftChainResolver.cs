using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.RecursiveCrafting.Core.Crafting;

/// <summary>
/// The recursive-chain judgement the recipe panel asks: is this recipe craftable from what the player holds
/// RIGHT NOW, reachable through other recipes, or not craftable at all — and through which steps.
///
/// <para>
/// The rules the resolver makes explicit:
/// </para>
/// <list type="number">
/// <item>An ingredient line consumes matching stacks until it has the units it asks for AND their summed
/// condition — the "two into one" case: two half-spent items satisfy a line that asks for one unit at
/// condition 1.</item>
/// <item>A missing line is satisfied by a recipe that produces it; that recipe's own ingredients are resolved
/// first, so a chain of any depth up to <see cref="MaximumDepth"/> resolves, and the steps come out in the
/// order they must run (deepest first, the target's own click last and not listed).</item>
/// <item>A recipe already on the current path is never used again: a cycle is not a chain. A line naming a
/// quality is only satisfied by held units carrying it (a crafted unit carries none in this model yet).</item>
/// <item>A line nothing can produce stops the chain, and the id is returned as the blocker so the panel can
/// say why instead of only "cannot be crafted".</item>
/// </list>
/// </summary>
public static class CraftChainResolver
{
	/// <summary>How deep a chain may go. Each step is a recipe run by a player; eight is already a long chain.</summary>
	public const int MaximumDepth = 8;

	/// <summary>Resolve one recipe against the player's held stacks.</summary>
	public static CraftResolution Resolve(CraftRecipe target, IReadOnlyCollection<CraftRecipe> recipes, IReadOnlyCollection<CraftStack> inventory)
	{
		if (target is null)
		{
			throw new ArgumentNullException(nameof(target));
		}

		if (recipes is null)
		{
			throw new ArgumentNullException(nameof(recipes));
		}

		if (inventory is null)
		{
			throw new ArgumentNullException(nameof(inventory));
		}

		var byResult = recipes
			.Where(recipe => !string.Equals(recipe.Id, target.Id, StringComparison.Ordinal))
			.GroupBy(recipe => recipe.ResultItemId, StringComparer.Ordinal)
			.ToDictionary(
				group => group.Key,
				group => group.OrderBy(recipe => recipe.Id, StringComparer.Ordinal).ToArray(),
				StringComparer.Ordinal);

		var attempt = new Attempt(
			[.. inventory.Select(stack => new HeldItem(stack.ItemId, stack.Amount, stack.Condition, stack.Quality))],
			[]);
		var path = new List<string> { target.Id };
		var blocker = TrySatisfyAll(target.Ingredients, depth: 0, path, byResult, attempt);
		if (blocker is not null)
		{
			return CraftResolution.Unreachable(blocker);
		}

		return attempt.Steps.Count == 0 ? CraftResolution.Now() : CraftResolution.ThroughSteps(attempt.Steps);
	}

	/// <summary>Null when every line was satisfied; otherwise the ingredient id that stopped the chain.</summary>
	private static string? TrySatisfyAll(
		IReadOnlyList<CraftIngredient> ingredients,
		int depth,
		List<string> path,
		Dictionary<string, CraftRecipe[]> byResult,
		Attempt attempt)
	{
		foreach (var ingredient in ingredients)
		{
			var blocker = TrySatisfyLine(ingredient, depth, path, byResult, attempt);
			if (blocker is not null)
			{
				return blocker;
			}
		}

		return null;
	}

	private static string? TrySatisfyLine(
		CraftIngredient ingredient,
		int depth,
		List<string> path,
		Dictionary<string, CraftRecipe[]> byResult,
		Attempt attempt)
	{
		while (true)
		{
			if (TryConsume(attempt.Held, ingredient))
			{
				return null;
			}

			if (depth >= MaximumDepth || !byResult.TryGetValue(ingredient.ItemId, out var candidates))
			{
				return ingredient.ItemId;
			}

			if (!TryCraftOne(candidates, depth, path, byResult, attempt))
			{
				return ingredient.ItemId;
			}
		}
	}

	/// <summary>Try the candidates in id order on a CLONE; the first that resolves is committed and returns true.</summary>
	private static bool TryCraftOne(
		CraftRecipe[] candidates,
		int depth,
		List<string> path,
		Dictionary<string, CraftRecipe[]> byResult,
		Attempt attempt)
	{
		foreach (var candidate in candidates)
		{
			if (path.Contains(candidate.Id) || candidate.ResultAmount < 1)
			{
				continue;
			}

			var trial = attempt.Clone();
			var morePath = new List<string>(path) { candidate.Id };
			if (TrySatisfyAll(candidate.Ingredients, depth + 1, morePath, byResult, trial) is not null)
			{
				continue;
			}

			trial.Held.Add(new HeldItem(candidate.ResultItemId, candidate.ResultAmount, 1f, quality: null));
			trial.Steps.Add(new CraftStep(candidate.Id, 1));
			attempt.ReplaceWith(trial);
			return true;
		}

		return false;
	}

	/// <summary>Consume matching stacks until the line is satisfied; commits the consumption only on success.</summary>
	private static bool TryConsume(List<HeldItem> held, CraftIngredient ingredient)
	{
		var matches = held
			.Where(stack => string.Equals(stack.Id, ingredient.ItemId, StringComparison.Ordinal))
			.Where(stack => ingredient.Quality is null || string.Equals(stack.Quality, ingredient.Quality, StringComparison.Ordinal))
			.OrderByDescending(stack => stack.Condition)
			.ThenBy(stack => stack.Quality ?? string.Empty, StringComparer.Ordinal)
			.ToList();

		var plan = new List<(HeldItem Stack, int Take)>();
		var units = 0;
		var condition = 0f;
		foreach (var stack in matches)
		{
			// Consume unit by unit: a line short of CONDITION takes a second, part-spent unit even when its
			// amount is already met — that is the "two into one" case, and a cap on the amount would stop one
			// unit early.
			var take = 0;
			while (take < stack.Amount && !(units >= ingredient.Amount && condition >= ingredient.TotalCondition))
			{
				take++;
				units++;
				condition += stack.Condition;
			}

			if (take > 0)
			{
				plan.Add((stack, take));
			}
		}

		if (units < ingredient.Amount || condition < ingredient.TotalCondition)
		{
			return false;
		}

		foreach (var (stack, take) in plan)
		{
			stack.Amount -= take;
		}

		held.RemoveAll(stack => stack.Amount <= 0);
		return true;
	}

	/// <summary>One mutable held stack; the public model is immutable, this is the resolver's working copy.</summary>
	private sealed class HeldItem(string id, int amount, float condition, string? quality)
	{
		internal string Id { get; } = id;

		internal int Amount { get; set; } = amount;

		internal float Condition { get; } = condition;

		internal string? Quality { get; } = quality;

		internal HeldItem Copy() => new(Id, Amount, Condition, Quality);
	}

	/// <summary>One attempt's working state: what is held and the steps taken so far.</summary>
	private sealed class Attempt(List<HeldItem> held, List<CraftStep> steps)
	{
		internal List<HeldItem> Held { get; private set; } = held;

		internal List<CraftStep> Steps { get; private set; } = steps;

		internal Attempt Clone() => new([.. Held.Select(stack => stack.Copy())], [.. Steps]);

		internal void ReplaceWith(Attempt other)
		{
			Held = other.Held;
			Steps = other.Steps;
		}
	}
}
