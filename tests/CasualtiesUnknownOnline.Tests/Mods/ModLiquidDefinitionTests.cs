using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed liquid content payload contract: a mod can serialize a
/// <see cref="ModLiquidDefinition"/> into the opaque content payload and the
/// Game Adapter liquid provider can decode it without a private format.
/// </summary>
public class ModLiquidDefinitionTests
{
	[Fact]
	public void RoundTrip_PreservesLiquidFields()
	{
		var original = new ModLiquidDefinition
		{
			DisplayName = "Green Goo",
			Description = "Sticky green liquid.",
			ColorR = 0.2f,
			ColorG = 0.8f,
			ColorB = 0.4f,
			ColorA = 0.9f,
			ValuePerLiter = 12.5f,
			HealthUsable = true,
			Injectable = true,
			InjectionSickness = 0.3f,
			LocaleFromItem = false,
			Qualities =
			[
				new ModLiquidQuality { Id = "chemical", Amount = 2f },
				new ModLiquidQuality { Id = "toxic", Amount = 0.5f }
			]
		};

		var restored = ModLiquidDefinition.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.Equal(original.DisplayName, restored!.DisplayName);
		Assert.Equal(original.Description, restored.Description);
		Assert.Equal(original.ColorR, restored.ColorR);
		Assert.Equal(original.ColorG, restored.ColorG);
		Assert.Equal(original.ColorB, restored.ColorB);
		Assert.Equal(original.ColorA, restored.ColorA);
		Assert.Equal(original.ValuePerLiter, restored.ValuePerLiter);
		Assert.True(restored.HealthUsable);
		Assert.True(restored.Injectable);
		Assert.Equal(original.InjectionSickness, restored.InjectionSickness);
		Assert.False(restored.LocaleFromItem);
		Assert.Equal(2, restored.Qualities.Count);
		Assert.Equal("chemical", restored.Qualities[0].Id);
		Assert.Equal(2f, restored.Qualities[0].Amount);
		Assert.Equal("toxic", restored.Qualities[1].Id);
	}

	[Fact]
	public void InvalidPayload_ReturnsNull()
	{
		Assert.Null(ModLiquidDefinition.FromPayload([]));
		Assert.Null(ModLiquidDefinition.FromPayload([1, 2, 3]));
		Assert.Null(ModLiquidDefinition.FromPayload(null!));
	}
}
