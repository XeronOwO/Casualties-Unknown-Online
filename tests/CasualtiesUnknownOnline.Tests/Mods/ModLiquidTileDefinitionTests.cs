using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed liquid-tile definition a mod hands to <see cref="IModContent"/>:
/// the identity its type fixes (<see cref="ModContentKind.LiquidTile"/>), the
/// custom-data collection where null means "none", the fluid/tint/consumption
/// defaults the definition declares and the layer helpers that need no payload.
/// Nothing serializes a definition any more, so this suite pins the answers the
/// definition itself owns.
/// </summary>
public class ModLiquidTileDefinitionTests
{
	[Fact]
	public void Identity_IsFixedByTheType()
	{
		var defaults = new ModLiquidTileDefinition();

		Assert.Equal(ModContentKind.LiquidTile, defaults.Kind);
		Assert.Empty(defaults.Id);
		Assert.Equal(1, defaults.SchemaVersion);

		var authored = new ModLiquidTileDefinition { Id = "test.toxic.pool", SchemaVersion = 2 };

		Assert.Equal("test.toxic.pool", authored.Id);
		Assert.Equal(2, authored.SchemaVersion);
	}

	[Fact]
	public void NullCollectionMembers_MeanNone()
	{
		var definition = new ModLiquidTileDefinition { CustomData = null! };

		Assert.Empty(definition.CustomData);
	}

	[Fact]
	public void DeclaredDefaults_HoldOnAFreshDefinition()
	{
		var definition = new ModLiquidTileDefinition();

		Assert.Empty(definition.DisplayName);
		Assert.Empty(definition.Description);
		Assert.Empty(definition.LiquidId);
		Assert.Empty(definition.FillLiquidId);
		Assert.Equal(0.6f, definition.Buoyancy);
		Assert.Equal(0.915f, definition.Drag);
		Assert.True(definition.PushBodies);
		Assert.Equal(20f, definition.WetnessPerSecond);
		Assert.Equal(ModLiquidTileVisualMode.ExistingLiquidPlusTint, definition.VisualMode);
		Assert.Equal(1, definition.VisualLiquidByte);
		Assert.Equal(1f, definition.TintR);
		Assert.Equal(1f, definition.TintG);
		Assert.Equal(1f, definition.TintB);
		Assert.Equal(1f, definition.TintA);
		Assert.Empty(definition.VisualAssetPath);
		Assert.Equal(ModLiquidTileDefinition.AllSpawnLayers, definition.SpawnLayers);
		Assert.Equal(128, definition.MaxFloodFill);
		Assert.True(definition.ConsumeOnDrink);
		Assert.True(definition.ConsumeOnFill);
	}

	[Fact]
	public void LayerHelpers_BehaveLikeTileLayers()
	{
		Assert.Equal(ModLiquidTileDefinition.AllSpawnLayers, ModLiquidTileDefinition.AllLayersExcept());
		Assert.Equal(~(1 | 4), ModLiquidTileDefinition.AllLayersExcept(1, 3));
		Assert.True(new ModLiquidTileDefinition { SpawnLayers = ModLiquidTileDefinition.AllSpawnLayers }.CanSpawnInLayer(0));
		Assert.False(new ModLiquidTileDefinition { SpawnLayers = 0 }.CanSpawnInLayer(0));
		Assert.True(new ModLiquidTileDefinition { SpawnLayers = ModLiquidTileDefinition.LayersToMask(2) }.CanSpawnInLayer(1));
		Assert.False(new ModLiquidTileDefinition { SpawnLayers = ModLiquidTileDefinition.LayersToMask(2) }.CanSpawnInLayer(0));
	}
}
