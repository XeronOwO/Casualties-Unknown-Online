using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed recipe content payload contract: mods can serialize a
/// <see cref="ModRecipeDefinition"/> into the opaque content payload and the
/// Game Adapter recipe provider can decode it without a private format.
/// </summary>
public class ModRecipeDefinitionTests
{
	[Fact]
	public void RoundTrip_PreservesRecipeFields()
	{
		var original = new ModRecipeDefinition
		{
			ResultItemId = "custom.rope",
			ResultIsLiquid = false,
			ResultAmount = 2,
			ResultCondition = 0.8f,
			DontDrainResultLiquid = true,
			Intelligence = 5,
			Category = ModRecipeCategory.Materials,
			IsRepair = false,
			Ingredients =
			[
				new ModRecipeIngredient
				{
					ItemId = "foliage",
					Quality = "",
					QualityAmount = 1f,
					MinimumCondition = 0.7f,
					DestroyItem = true
				},
				new ModRecipeIngredient
				{
					ItemId = "",
					Quality = "cutting",
					QualityAmount = 1.5f,
					MinimumCondition = 0.9f,
					DestroyItem = false
				}
			]
		};

		var restored = ModRecipeDefinition.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.Equal(original.ResultItemId, restored!.ResultItemId);
		Assert.False(restored.ResultIsLiquid);
		Assert.Equal(original.ResultAmount, restored.ResultAmount);
		Assert.Equal(original.ResultCondition, restored.ResultCondition);
		Assert.True(restored.DontDrainResultLiquid);
		Assert.Equal(original.Intelligence, restored.Intelligence);
		Assert.Equal(original.Category, restored.Category);
		Assert.False(restored.IsRepair);
		Assert.Equal(2, restored.Ingredients.Count);
		Assert.Equal("foliage", restored.Ingredients[0].ItemId);
		Assert.Equal(0.7f, restored.Ingredients[0].MinimumCondition);
		Assert.Equal("cutting", restored.Ingredients[1].Quality);
		Assert.Equal(1.5f, restored.Ingredients[1].QualityAmount);
		Assert.False(restored.Ingredients[1].DestroyItem);
	}

	[Fact]
	public void InvalidPayload_ReturnsNull()
	{
		Assert.Null(ModRecipeDefinition.FromPayload([]));
		Assert.Null(ModRecipeDefinition.FromPayload([1, 2, 3]));
		Assert.Null(ModRecipeDefinition.FromPayload(null!));
	}
}
