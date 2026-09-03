using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed building content payload contract: a mod can serialize a
/// <see cref="ModBuildingDefinition"/> into the opaque byte payload and the
/// Runtime/Game Adapter can read it back without a private format.
/// </summary>
public class ModBuildingDefinitionTests
{
	[Fact]
	public void RoundTrip_PreservesCoreFields()
	{
		var original = new ModBuildingDefinition
		{
			DisplayName = "Custom Crate",
			Description = "A mod-authored crate.",
			TemplateId = "crate",
			Health = 120f,
			RequireGround = true,
			Animal = false,
			CantHit = false,
			Metallic = true,
			IgnoreBodyOptimize = false,
			DropChanceMultiplier = 1.5f,
			GuaranteedDropAmount = 2,
			SpawnComponents = ["Example.CrateBehaviour, ExampleMod"],
			DropOnDestroy =
			[
				new ModBuildingDrop { ItemId = "scrap", Chance = 0.5f, MinCondition = 0.2f, MaxCondition = 0.8f }
			],
			AlwaysDrop =
			[
				new ModBuildingDrop { ItemId = "crate_lid", Chance = 1f, MinCondition = 1f, MaxCondition = 1f }
			],
			ItemCategoriesToAdd = ["tools", "medical"],
			SpawnMinPerChunk = 0.01f,
			SpawnMaxPerChunk = 0.05f,
			SpawnLayers = ModBuildingDefinition.LayersToMask(2, 3),
			GenerationStyle = ModBuildingGenerationStyle.Standard,
			Placement = ModBuildingPlacement.Floor,
			SpawnInGround = true,
			SurfaceOffset = 0.35f,
			RandomFlip = false,
			CustomData = new Dictionary<string, string>
			{
				["mod.metadata"] = "kept"
			}
		};

		var restored = ModBuildingDefinition.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.Equal(original.DisplayName, restored!.DisplayName);
		Assert.Equal(original.Description, restored.Description);
		Assert.Equal(original.TemplateId, restored.TemplateId);
		Assert.Equal(original.Health, restored.Health);
		Assert.Equal(original.RequireGround, restored.RequireGround);
		Assert.Equal(original.Animal, restored.Animal);
		Assert.Equal(original.CantHit, restored.CantHit);
		Assert.Equal(original.Metallic, restored.Metallic);
		Assert.Equal(original.IgnoreBodyOptimize, restored.IgnoreBodyOptimize);
		Assert.Equal(original.DropChanceMultiplier, restored.DropChanceMultiplier);
		Assert.Equal(original.GuaranteedDropAmount, restored.GuaranteedDropAmount);
		Assert.Equal(original.SpawnComponents, restored.SpawnComponents);
		Assert.Single(restored.DropOnDestroy);
		Assert.Equal("scrap", restored.DropOnDestroy[0].ItemId);
		Assert.Equal(0.5f, restored.DropOnDestroy[0].Chance);
		Assert.Single(restored.AlwaysDrop);
		Assert.Equal("crate_lid", restored.AlwaysDrop[0].ItemId);
		Assert.Equal(["tools", "medical"], restored.ItemCategoriesToAdd);
		Assert.Equal(original.SpawnMinPerChunk, restored.SpawnMinPerChunk);
		Assert.Equal(original.SpawnMaxPerChunk, restored.SpawnMaxPerChunk);
		Assert.Equal(original.SpawnLayers, restored.SpawnLayers);
		Assert.Equal(original.GenerationStyle, restored.GenerationStyle);
		Assert.Equal(original.Placement, restored.Placement);
		Assert.True(restored.SpawnInGround);
		Assert.Equal(original.SurfaceOffset, restored.SurfaceOffset);
		Assert.False(restored.RandomFlip);
		Assert.Equal("kept", restored.CustomData["mod.metadata"]);
	}

	[Fact]
	public void RoundTrip_PreservesNullOptionalOverrides()
	{
		var original = new ModBuildingDefinition
		{
			TemplateId = "rustle",
			Health = null,
			RequireGround = null,
			Animal = null,
			CantHit = null,
			Metallic = null,
			IgnoreBodyOptimize = null,
			DropChanceMultiplier = null,
			GuaranteedDropAmount = null,
			SpawnMinPerChunk = null,
			SpawnMaxPerChunk = null,
			SurfaceOffset = null,
			RandomFlip = null
		};

		var restored = ModBuildingDefinition.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.Null(restored!.Health);
		Assert.Null(restored.RequireGround);
		Assert.Null(restored.Animal);
		Assert.Null(restored.CantHit);
		Assert.Null(restored.Metallic);
		Assert.Null(restored.IgnoreBodyOptimize);
		Assert.Null(restored.DropChanceMultiplier);
		Assert.Null(restored.GuaranteedDropAmount);
		Assert.Null(restored.SpawnMinPerChunk);
		Assert.Null(restored.SpawnMaxPerChunk);
		Assert.Null(restored.SurfaceOffset);
		Assert.Null(restored.RandomFlip);
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

	[Fact]
	public void InvalidPayload_ReturnsNull()
	{
		Assert.Null(ModBuildingDefinition.FromPayload([]));
		Assert.Null(ModBuildingDefinition.FromPayload([1, 2, 3]));
		Assert.Null(ModBuildingDefinition.FromPayload(null!));
	}

	[Fact]
	public void ModBuildingDrop_RollCondition_ClampsIntoSegment()
	{
		var drop = new ModBuildingDrop { MinCondition = 0.2f, MaxCondition = 0.8f };
		Assert.Equal(0.2f, drop.RollCondition(0f));
		Assert.Equal(0.5f, drop.RollCondition(0.5f));
		Assert.Equal(0.8f, drop.RollCondition(1f));
	}
}
