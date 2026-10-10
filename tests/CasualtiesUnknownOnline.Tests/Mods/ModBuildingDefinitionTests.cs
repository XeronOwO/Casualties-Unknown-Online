using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed building definition a mod hands to <see cref="IModContent"/>: the
/// identity its type fixes (<see cref="ModContentKind.Building"/>), the
/// collection members where null means "none", the defaults the definition and
/// its drop entries declare, and the layer-mask helpers that need no payload.
/// Nothing serializes a definition any more, so this suite pins the answers the
/// definition itself owns.
/// </summary>
public class ModBuildingDefinitionTests
{
	[Fact]
	public void Identity_IsFixedByTheType()
	{
		var defaults = new ModBuildingDefinition();

		Assert.Equal(ModContentKind.Building, defaults.Kind);
		Assert.Empty(defaults.Id);
		Assert.Equal(1, defaults.SchemaVersion);

		var authored = new ModBuildingDefinition { Id = "test.crate", SchemaVersion = 4 };

		Assert.Equal("test.crate", authored.Id);
		Assert.Equal(4, authored.SchemaVersion);
	}

	[Fact]
	public void NullCollectionMembers_MeanNone()
	{
		var definition = new ModBuildingDefinition
		{
			SpawnComponents = null!,
			CustomData = null!,
			DropOnDestroy = null!,
			AlwaysDrop = null!,
			ItemCategoriesToAdd = null!
		};

		Assert.Empty(definition.SpawnComponents);
		Assert.Empty(definition.CustomData);
		Assert.Empty(definition.DropOnDestroy);
		Assert.Empty(definition.AlwaysDrop);
		Assert.Empty(definition.ItemCategoriesToAdd);
	}

	[Fact]
	public void DeclaredDefaults_HoldOnAFreshDefinition()
	{
		var definition = new ModBuildingDefinition();

		Assert.Empty(definition.DisplayName);
		Assert.Empty(definition.Description);
		Assert.Empty(definition.TemplateId);
		Assert.Equal(ModBuildingDefinition.AllSpawnLayers, definition.SpawnLayers);
		Assert.Equal(ModBuildingGenerationStyle.None, definition.GenerationStyle);
		Assert.Equal(ModBuildingPlacement.Floor, definition.Placement);
	}

	[Fact]
	public void ModBuildingDropDefaults_HoldOnAFreshDrop()
	{
		var drop = new ModBuildingDrop();

		Assert.Empty(drop.ItemId);
		Assert.Equal(1f, drop.Chance);
		Assert.Equal(1f, drop.MaxCondition);
	}

	[Fact]
	public void LayerMaskHelpers_AreConsistent()
	{
		Assert.Equal(ModBuildingDefinition.AllSpawnLayers, ModBuildingDefinition.AllLayersExcept());
		Assert.False(new ModBuildingDefinition { SpawnLayers = ModBuildingDefinition.AllLayersExcept(1) }.CanSpawnInLayer(0));
		Assert.True(new ModBuildingDefinition { SpawnLayers = ModBuildingDefinition.AllLayersExcept(1) }.CanSpawnInLayer(1));
		Assert.True(new ModBuildingDefinition { SpawnLayers = ModBuildingDefinition.LayersToMask(1) }.CanSpawnInLayer(0));
		Assert.False(new ModBuildingDefinition { SpawnLayers = ModBuildingDefinition.LayersToMask(2) }.CanSpawnInLayer(0));
		Assert.False(new ModBuildingDefinition { SpawnLayers = 0 }.CanSpawnInLayer(0));
	}
}
