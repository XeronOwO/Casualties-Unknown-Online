using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed recipe definition a mod hands to <see cref="IModContent"/>: the
/// identity its type fixes (<see cref="ModContentKind.Recipe"/>), the ingredient
/// list where null means "none" and the result/ingredient defaults the
/// definition and its ingredients declare. Nothing serializes a definition any
/// more, so this suite pins the answers the definition itself owns.
/// </summary>
public class ModRecipeDefinitionTests
{
	[Fact]
	public void Identity_IsFixedByTheType()
	{
		var defaults = new ModRecipeDefinition();

		Assert.Equal(ModContentKind.Recipe, defaults.Kind);
		Assert.Empty(defaults.Id);
		Assert.Equal(1, defaults.SchemaVersion);

		var authored = new ModRecipeDefinition { Id = "test.rope", SchemaVersion = 2 };

		Assert.Equal("test.rope", authored.Id);
		Assert.Equal(2, authored.SchemaVersion);
	}

	[Fact]
	public void NullCollectionMembers_MeanNone()
	{
		var definition = new ModRecipeDefinition { Ingredients = null! };

		Assert.Empty(definition.Ingredients);
	}

	[Fact]
	public void DeclaredDefaults_HoldOnAFreshDefinition()
	{
		var definition = new ModRecipeDefinition();

		Assert.Empty(definition.ResultItemId);
		Assert.Equal(1, definition.ResultAmount);
		Assert.Equal(1f, definition.ResultCondition);
		Assert.Equal(ModRecipeCategory.Materials, definition.Category);
	}

	[Fact]
	public void IngredientDefaults_HoldOnAFreshIngredient()
	{
		var ingredient = new ModRecipeIngredient();

		Assert.Empty(ingredient.ItemId);
		Assert.Empty(ingredient.Quality);
		Assert.Equal(1f, ingredient.QualityAmount);
		Assert.Equal(0.9f, ingredient.MinimumCondition);
		Assert.True(ingredient.DestroyItem);
	}
}
