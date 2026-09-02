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
	public void RoundTrip_PreservesWorldGenerationAndDrops()
	{
		var original = new ModTileDefinition
		{
			DisplayName = "Auric Ore",
			SpawnAmount = 1.5f,
			SpawnLayers = ModTileDefinition.LayersToMask(4, 5, 6),
			GenerationStyle = ModTileGenerationStyle.HeavyVeins | ModTileGenerationStyle.Inner,
			Drops =
			[
				new ModTileDrop { ItemId = "auricfragment", Chance = 0.8f, MinCondition = 0.2f, MaxCondition = 0.9f }
			]
		};

		var restored = ModTileDefinition.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.Equal(original.SpawnAmount, restored!.SpawnAmount);
		Assert.Equal(original.SpawnLayers, restored.SpawnLayers);
		Assert.Equal(original.GenerationStyle, restored.GenerationStyle);
		var drop = Assert.Single(restored.Drops);
		Assert.Equal("auricfragment", drop.ItemId);
		Assert.Equal(0.8f, drop.Chance);
		Assert.Equal(0.2f, drop.MinCondition);
		Assert.Equal(0.9f, drop.MaxCondition);
	}

	[Fact]
	public void CanSpawnInLayer_HandlesAllZeroAndDepthBounds()
	{
		var all = new ModTileDefinition { SpawnLayers = ModTileDefinition.AllSpawnLayers };
		var none = new ModTileDefinition { SpawnLayers = 0 };
		var layers = new ModTileDefinition { SpawnLayers = ModTileDefinition.LayersToMask(1, 3) };

		Assert.True(all.CanSpawnInLayer(0));
		Assert.True(all.CanSpawnInLayer(30));
		Assert.False(all.CanSpawnInLayer(-1));
		Assert.False(none.CanSpawnInLayer(0));
		Assert.True(layers.CanSpawnInLayer(0));
		Assert.False(layers.CanSpawnInLayer(1));
		Assert.True(layers.CanSpawnInLayer(2));
		Assert.False(layers.CanSpawnInLayer(31));
	}

	[Fact]
	public void LayerMaskHelpers_BuildExpectedMasks()
	{
		Assert.Equal(1 | 4, ModTileDefinition.LayersToMask(1, 3));
		Assert.Equal(ModTileDefinition.AllSpawnLayers, ModTileDefinition.AllLayersExcept());
		Assert.Equal(~(1 | 4), ModTileDefinition.AllLayersExcept(1, 3));
	}

	[Fact]
	public void InvalidPayload_ReturnsNull()
	{
		Assert.Null(ModTileDefinition.FromPayload([]));
		Assert.Null(ModTileDefinition.FromPayload([1, 2, 3]));
		Assert.Null(ModTileDefinition.FromPayload(null!));
	}
}
