using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed liquid definition a mod hands to <see cref="IModContent"/>: the
/// identity its type fixes (<see cref="ModContentKind.Liquid"/>), the qualities
/// collection where null means "none" and the tint/injection defaults the
/// definition declares. Nothing serializes a definition any more, so this suite
/// pins the answers the definition itself owns.
/// </summary>
public class ModLiquidDefinitionTests
{
	[Fact]
	public void Identity_IsFixedByTheType()
	{
		var defaults = new ModLiquidDefinition();

		Assert.Equal(ModContentKind.Liquid, defaults.Kind);
		Assert.Empty(defaults.Id);
		Assert.Equal(1, defaults.SchemaVersion);

		var authored = new ModLiquidDefinition { Id = "test.goo", SchemaVersion = 5 };

		Assert.Equal("test.goo", authored.Id);
		Assert.Equal(5, authored.SchemaVersion);
	}

	[Fact]
	public void NullCollectionMembers_MeanNone()
	{
		var definition = new ModLiquidDefinition { Qualities = null! };

		Assert.Empty(definition.Qualities);
	}

	[Fact]
	public void DeclaredDefaults_HoldOnAFreshDefinition()
	{
		var definition = new ModLiquidDefinition();

		Assert.Empty(definition.DisplayName);
		Assert.Empty(definition.Description);
		Assert.Equal(1f, definition.ColorR);
		Assert.Equal(1f, definition.ColorG);
		Assert.Equal(1f, definition.ColorB);
		Assert.Equal(1f, definition.ColorA);
		Assert.Equal(1f, definition.InjectionSickness);
	}
}
