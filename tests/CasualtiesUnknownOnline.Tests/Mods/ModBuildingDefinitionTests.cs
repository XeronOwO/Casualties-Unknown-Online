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
			GuaranteedDropAmount = null
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
	}

	[Fact]
	public void InvalidPayload_ReturnsNull()
	{
		Assert.Null(ModBuildingDefinition.FromPayload([]));
		Assert.Null(ModBuildingDefinition.FromPayload([1, 2, 3]));
		Assert.Null(ModBuildingDefinition.FromPayload(null!));
	}
}
