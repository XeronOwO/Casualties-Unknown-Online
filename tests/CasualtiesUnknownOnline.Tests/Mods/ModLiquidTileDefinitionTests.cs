using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed liquid-tile content payload contract: a mod can serialize a
/// <see cref="ModLiquidTileDefinition"/> into the opaque content payload and
/// the Game Adapter liquid-tile provider can decode it without a private
/// format.
/// </summary>
public class ModLiquidTileDefinitionTests
{
	[Fact]
	public void RoundTrip_PreservesLiquidTileFields()
	{
		var original = new ModLiquidTileDefinition
		{
			DisplayName = "Toxic Pool",
			Description = "A pool of toxic liquid.",
			LiquidId = "toxin",
			FillLiquidId = "toxin",
			Buoyancy = 0.4f,
			Drag = 0.8f,
			PushBodies = false,
			WetnessPerSecond = 30f,
			TemperaturePerSecond = -1f,
			SicknessPerSecond = 2f,
			DirtynessPerSecond = 3f,
			DisinfectPerSecond = 0.5f,
			SlipPerSecond = 0.1f,
			RagdollBarDrainPerSecond = 0.2f,
			VisualMode = ModLiquidTileVisualMode.ExistingLiquidPlusTint,
			VisualLiquidByte = 3,
			TintR = 0.2f,
			TintG = 0.8f,
			TintB = 0.4f,
			TintA = 0.9f,
			VisualAssetPath = "pools/toxic",
			SpawnAmount = 4f,
			SpawnLayers = ModLiquidTileDefinition.LayersToMask(2, 4),
			MaxFloodFill = 256,
			ConsumeOnDrink = true,
			ConsumeOnFill = false
		};

		var restored = ModLiquidTileDefinition.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.Equal(original.DisplayName, restored!.DisplayName);
		Assert.Equal(original.Description, restored.Description);
		Assert.Equal(original.LiquidId, restored.LiquidId);
		Assert.Equal(original.FillLiquidId, restored.FillLiquidId);
		Assert.Equal(original.Buoyancy, restored.Buoyancy);
		Assert.Equal(original.Drag, restored.Drag);
		Assert.False(restored.PushBodies);
		Assert.Equal(original.WetnessPerSecond, restored.WetnessPerSecond);
		Assert.Equal(original.TemperaturePerSecond, restored.TemperaturePerSecond);
		Assert.Equal(original.SicknessPerSecond, restored.SicknessPerSecond);
		Assert.Equal(original.DirtynessPerSecond, restored.DirtynessPerSecond);
		Assert.Equal(original.DisinfectPerSecond, restored.DisinfectPerSecond);
		Assert.Equal(original.SlipPerSecond, restored.SlipPerSecond);
		Assert.Equal(original.RagdollBarDrainPerSecond, restored.RagdollBarDrainPerSecond);
		Assert.Equal(ModLiquidTileVisualMode.ExistingLiquidPlusTint, restored.VisualMode);
		Assert.Equal(3, restored.VisualLiquidByte);
		Assert.Equal(original.TintR, restored.TintR);
		Assert.Equal(original.TintG, restored.TintG);
		Assert.Equal(original.TintB, restored.TintB);
		Assert.Equal(original.TintA, restored.TintA);
		Assert.Equal("pools/toxic", restored.VisualAssetPath);
		Assert.Equal(4f, restored.SpawnAmount);
		Assert.Equal(original.SpawnLayers, restored.SpawnLayers);
		Assert.Equal(256, restored.MaxFloodFill);
		Assert.True(restored.ConsumeOnDrink);
		Assert.False(restored.ConsumeOnFill);
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

	[Fact]
	public void InvalidPayload_ReturnsNull()
	{
		Assert.Null(ModLiquidTileDefinition.FromPayload([]));
		Assert.Null(ModLiquidTileDefinition.FromPayload([1, 2, 3]));
		Assert.Null(ModLiquidTileDefinition.FromPayload(null!));
	}
}
