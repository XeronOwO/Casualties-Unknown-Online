using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed item content payload contract: a mod can serialize a
/// <see cref="ModItemDefinition"/> into the opaque byte payload and the
/// Runtime/Game Adapter can read it back without a private format.
/// </summary>
public class ModItemDefinitionTests
{
	[Fact]
	public void RoundTrip_PreservesCoreFields()
	{
		var original = new ModItemDefinition
		{
			DisplayName = "Test Shard",
			Description = "A brittle shard.",
			Category = "misc",
			Weight = 0.5f,
			Value = 7,
			Usable = true,
			UsableWithLmb = true,
			Wearable = false,
			DestroyAtZeroCondition = true,
			Tags = "test,shard",
			SpawnFrequency = 3,
			TemplateId = "stone",
			SpawnComponents = ["Example.ShardBehaviour, ExampleMod"],
			WorldSpawnPerChunk = 0.5f,
			DropSources = ModItemDropSource.Corpse | ModItemDropSource.Trader1,
			CustomData = new Dictionary<string, string>
			{
				["mod.metadata"] = "kept"
			}
		};

		var restored = ModItemDefinition.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.Equal(original.DisplayName, restored!.DisplayName);
		Assert.Equal(original.Description, restored.Description);
		Assert.Equal(original.Category, restored.Category);
		Assert.Equal(original.Weight, restored.Weight);
		Assert.Equal(original.Value, restored.Value);
		Assert.True(restored.Usable);
		Assert.True(restored.UsableWithLmb);
		Assert.False(restored.Wearable);
		Assert.True(restored.DestroyAtZeroCondition);
		Assert.Equal(original.Tags, restored.Tags);
		Assert.Equal(original.SpawnFrequency, restored.SpawnFrequency);
		Assert.Equal(original.TemplateId, restored.TemplateId);
		Assert.Equal(original.SpawnComponents, restored.SpawnComponents);
		Assert.Equal(original.WorldSpawnPerChunk, restored.WorldSpawnPerChunk);
		Assert.Equal(original.DropSources, restored.DropSources);
		Assert.Equal("kept", restored.CustomData["mod.metadata"]);
	}

	[Fact]
	public void InvalidPayload_ReturnsNull()
	{
		Assert.Null(ModItemDefinition.FromPayload([]));
		Assert.Null(ModItemDefinition.FromPayload([1, 2, 3]));
		Assert.Null(ModItemDefinition.FromPayload(null!));
	}
}
