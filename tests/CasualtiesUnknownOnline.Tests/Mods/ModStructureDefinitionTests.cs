using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed structure content payload contract: a mod can serialize a
/// <see cref="ModStructureDefinition"/> into the opaque byte payload and the
/// Runtime/Game Adapter can read it back without a private format.
/// </summary>
public class ModStructureDefinitionTests
{
	[Fact]
	public void RoundTrip_PreservesCoreFields()
	{
		var original = new ModStructureDefinition
		{
			DisplayName = "Auric Shrine",
			Description = "A small shrine built from auric ore.",
			Width = 3,
			Height = 2,
			Rows =
			[
				"#.#",
				"###"
			],
			VanillaBlocks = new Dictionary<string, int>
			{
				["#"] = 5
			},
			TileIds = new Dictionary<string, string>
			{
				["@"] = "custom.auric"
			},
			SpawnCounts = [2, 1, 0, 1, 3],
			CustomData = new Dictionary<string, string>
			{
				["mod.metadata"] = "kept"
			}
		};

		var restored = ModStructureDefinition.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.Equal(original.DisplayName, restored!.DisplayName);
		Assert.Equal(original.Description, restored.Description);
		Assert.Equal(original.Width, restored.Width);
		Assert.Equal(original.Height, restored.Height);
		Assert.Equal(original.Rows, restored.Rows);
		Assert.Equal(5, restored.VanillaBlocks["#"]);
		Assert.Equal("custom.auric", restored.TileIds["@"]);
		Assert.Equal(original.SpawnCounts, restored.SpawnCounts);
		Assert.Equal("kept", restored.CustomData["mod.metadata"]);
	}

	[Fact]
	public void RoundTrip_PreservesEmptyOptionalMaps()
	{
		var original = new ModStructureDefinition
		{
			Width = 1,
			Height = 1,
			Rows = ["."]
		};

		var restored = ModStructureDefinition.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.Single(restored!.Rows);
		Assert.Equal(".", restored.Rows[0]);
		Assert.Empty(restored.VanillaBlocks);
		Assert.Empty(restored.TileIds);
		Assert.Empty(restored.SpawnCounts);
	}

	[Fact]
	public void InvalidPayload_ReturnsNull()
	{
		Assert.Null(ModStructureDefinition.FromPayload([]));
		Assert.Null(ModStructureDefinition.FromPayload([1, 2, 3]));
		Assert.Null(ModStructureDefinition.FromPayload(null!));
	}
}
