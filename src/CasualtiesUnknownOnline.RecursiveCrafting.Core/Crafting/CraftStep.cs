using System;

namespace CasualtiesUnknownOnline.RecursiveCrafting.Core.Crafting;

/// <summary>One step of a resolved chain: craft this recipe this many times, before the step that needs it.</summary>
public sealed class CraftStep(string recipeId, int times)
{
	/// <summary>The recipe to craft.</summary>
	public string RecipeId { get; } = recipeId;

	/// <summary>How many times it must run.</summary>
	public int Times { get; } = times;

	/// <inheritdoc />
	public override bool Equals(object? obj) =>
		obj is CraftStep other
		&& Times == other.Times
		&& string.Equals(RecipeId, other.RecipeId, StringComparison.Ordinal);

	/// <inheritdoc />
	public override int GetHashCode() => (StringComparer.Ordinal.GetHashCode(RecipeId) * 397) ^ Times;
}
