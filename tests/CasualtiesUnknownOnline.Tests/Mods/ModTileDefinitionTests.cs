using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed tile content payload contract: a mod can serialize a
/// <see cref="ModTileDefinition"/> into the opaque byte payload and the
/// Runtime/Game Adapter can read it back without a private format.
/// </summary>
public class ModTileDefinitionTests
{
	[Fact]
	public void RoundTrip_PreservesCoreFields()
	{
		var original = new ModTileDefinition
		{
			DisplayName = "Auric Ore",
			Description = "A rare conductive ore.",
			TemplateTileIndex = 1,
			SpritePath = "CustomTiles/auric",
			TileName = "AuricTile",
			Health = 777f,
			HitSound = "crystal",
			StepSound = "Rock",
			SleepQuality = ModTileSleepQuality.Bad,
			NoVariation = true,
			Metallic = true,
			Toxicity = 2.5f,
			Slippery = true,
			ColorR = 0.25f,
			ColorG = 0.5f,
			ColorB = 0.75f,
			ColorA = 1f,
			ColliderType = ModTileColliderType.Grid,
			CustomData = new Dictionary<string, string>
			{
				["mod.metadata"] = "kept"
			}
		};

		var restored = ModTileDefinition.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.Equal(original.DisplayName, restored!.DisplayName);
		Assert.Equal(original.Description, restored.Description);
		Assert.Equal(original.TemplateTileIndex, restored.TemplateTileIndex);
		Assert.Equal(original.SpritePath, restored.SpritePath);
		Assert.Equal(original.TileName, restored.TileName);
		Assert.Equal(original.Health, restored.Health);
		Assert.Equal(original.HitSound, restored.HitSound);
		Assert.Equal(original.StepSound, restored.StepSound);
		Assert.Equal(original.SleepQuality, restored.SleepQuality);
		Assert.True(restored.NoVariation);
		Assert.True(restored.Metallic);
		Assert.Equal(original.Toxicity, restored.Toxicity);
		Assert.True(restored.Slippery);
		Assert.Equal(original.ColorR, restored.ColorR);
		Assert.Equal(original.ColorG, restored.ColorG);
		Assert.Equal(original.ColorB, restored.ColorB);
		Assert.Equal(original.ColorA, restored.ColorA);
		Assert.Equal(original.ColliderType, restored.ColliderType);
		Assert.Equal("kept", restored.CustomData["mod.metadata"]);
	}

	[Fact]
	public void RoundTrip_PreservesMissingOptionalVisualSource()
	{
		var original = new ModTileDefinition
		{
			DisplayName = "Plain",
			TemplateTileIndex = null,
			SpritePath = "",
			Health = 50f
		};

		var restored = ModTileDefinition.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.Null(restored!.TemplateTileIndex);
		Assert.Empty(restored.SpritePath);
		Assert.Equal(50f, restored.Health);
	}

	[Fact]
	public void InvalidPayload_ReturnsNull()
	{
		Assert.Null(ModTileDefinition.FromPayload([]));
		Assert.Null(ModTileDefinition.FromPayload([1, 2, 3]));
		Assert.Null(ModTileDefinition.FromPayload(null!));
	}
}
