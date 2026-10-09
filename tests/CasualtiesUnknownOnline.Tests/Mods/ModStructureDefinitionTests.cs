using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed structure definition a mod hands to <see cref="IModContent"/>: the
/// identity its type fixes (<see cref="ModContentKind.Structure"/>), the grid and
/// marker collections where null means "none", the 1x1 defaults the definition
/// declares, and the depth lookup that needs no payload. Nothing serializes a
/// definition any more, so this suite pins the answers the definition itself
/// owns.
/// </summary>
public class ModStructureDefinitionTests
{
	[Fact]
	public void Identity_IsFixedByTheType()
	{
		var defaults = new ModStructureDefinition();

		Assert.Equal(ModContentKind.Structure, defaults.Kind);
		Assert.Empty(defaults.Id);
		Assert.Equal(1, defaults.SchemaVersion);

		var authored = new ModStructureDefinition { Id = "test.shrine", SchemaVersion = 2 };

		Assert.Equal("test.shrine", authored.Id);
		Assert.Equal(2, authored.SchemaVersion);
	}

	[Fact]
	public void NullCollectionMembers_MeanNone()
	{
		var definition = new ModStructureDefinition
		{
			Rows = null!,
			VanillaBlocks = null!,
			TileIds = null!,
			SpawnCounts = null!,
			CustomData = null!
		};

		Assert.Empty(definition.Rows);
		Assert.Empty(definition.VanillaBlocks);
		Assert.Empty(definition.TileIds);
		Assert.Empty(definition.SpawnCounts);
		Assert.Empty(definition.CustomData);
	}

	[Fact]
	public void DeclaredDefaults_HoldOnAFreshDefinition()
	{
		var definition = new ModStructureDefinition();

		Assert.Empty(definition.DisplayName);
		Assert.Empty(definition.Description);
		Assert.Equal(1, definition.Width);
		Assert.Equal(1, definition.Height);
	}

	[Fact]
	public void TryGetSpawnCount_ReturnsTheDepthValue()
	{
		var definition = new ModStructureDefinition
		{
			SpawnCounts = [2, 1, 0, 3, 5]
		};

		Assert.True(definition.TryGetSpawnCount(1, out var one));
		Assert.Equal(1, one);
		Assert.True(definition.TryGetSpawnCount(4, out var four));
		Assert.Equal(5, four);
	}

	[Fact]
	public void TryGetSpawnCount_MissingOrEmptyDepth_ReturnsFalse()
	{
		var definition = new ModStructureDefinition
		{
			SpawnCounts = [2, 3]
		};

		Assert.False(definition.TryGetSpawnCount(2, out _));
		Assert.False(definition.TryGetSpawnCount(-1, out _));
		Assert.False(new ModStructureDefinition().TryGetSpawnCount(0, out _));
	}

	[Fact]
	public void TryGetSpawnCount_NegativeValue_IsClampedToZero()
	{
		var definition = new ModStructureDefinition
		{
			SpawnCounts = [4, -1]
		};

		Assert.True(definition.TryGetSpawnCount(1, out var count));
		Assert.Equal(0, count);
	}
}
