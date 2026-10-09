using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed tile definition a mod hands to <see cref="IModContent"/>: the
/// identity its type fixes (<see cref="ModContentKind.Tile"/>), the collection
/// members where null means "none", the defaults the definition and its drop
/// entries declare, and the layer-mask helpers that need no payload. Nothing
/// serializes a definition any more, so this suite pins the answers the
/// definition itself owns.
/// </summary>
public class ModTileDefinitionTests
{
	[Fact]
	public void Identity_IsFixedByTheType()
	{
		var defaults = new ModTileDefinition();

		Assert.Equal(ModContentKind.Tile, defaults.Kind);
		Assert.Empty(defaults.Id);
		Assert.Equal(1, defaults.SchemaVersion);

		var authored = new ModTileDefinition { Id = "test.auric", SchemaVersion = 2 };

		Assert.Equal("test.auric", authored.Id);
		Assert.Equal(2, authored.SchemaVersion);
	}

	[Fact]
	public void NullCollectionMembers_MeanNone()
	{
		var definition = new ModTileDefinition
		{
			CustomData = null!,
			Drops = null!
		};

		Assert.Empty(definition.CustomData);
		Assert.Empty(definition.Drops);
	}

	[Fact]
	public void DeclaredDefaults_HoldOnAFreshDefinition()
	{
		var definition = new ModTileDefinition();

		Assert.Empty(definition.DisplayName);
		Assert.Empty(definition.Description);
		Assert.Empty(definition.SpritePath);
		Assert.Empty(definition.TileName);
		Assert.Equal(100f, definition.Health);
		Assert.Equal("rock", definition.HitSound);
		Assert.Equal("Rock", definition.StepSound);
		Assert.Equal(ModTileSleepQuality.Bad, definition.SleepQuality);
		Assert.Equal(1f, definition.ColorR);
		Assert.Equal(1f, definition.ColorG);
		Assert.Equal(1f, definition.ColorB);
		Assert.Equal(1f, definition.ColorA);
		Assert.Equal(ModTileColliderType.Grid, definition.ColliderType);
		Assert.Equal(ModTileDefinition.AllSpawnLayers, definition.SpawnLayers);
		Assert.Equal(ModTileGenerationStyle.Vein, definition.GenerationStyle);
	}

	[Fact]
	public void ModTileDropDefaults_HoldOnAFreshDrop()
	{
		var drop = new ModTileDrop();

		Assert.Empty(drop.ItemId);
		Assert.Equal(1f, drop.Chance);
		Assert.Equal(1f, drop.MaxCondition);
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
}
