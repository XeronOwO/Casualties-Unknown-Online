using CasualtiesUnknownOnline.Runtime.GameAdapter;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameAdapter;

/// <summary>
/// The character table the snapshot and the clone share: the game's three characters by the index
/// the pre-run screen stores, and the wire form the snapshot carries (<c>index + 1</c>, 0 = not
/// reported). A wrong mapping here would silently dress a teammate in the viewer's body, so the
/// table, the wire form and the body-name reading are pinned one fact each.
/// </summary>
public sealed class CharacterIndexTests
{
	[Fact]
	public void TheWireFormRoundTripsEachCharacter()
	{
		for (var index = 0; index < CharacterIndex.Count; index++)
		{
			Assert.Equal(index, CharacterIndex.FromWire(CharacterIndex.ToWire(index)));
		}

		Assert.Equal(1, CharacterIndex.ToWire(0));
		Assert.Equal(3, CharacterIndex.ToWire(2));
	}

	[Fact]
	public void AnUnknownIndexOrWireValueReadsAsUnknown()
	{
		Assert.Equal(0, CharacterIndex.ToWire(CharacterIndex.Unknown));
		Assert.Equal(CharacterIndex.Unknown, CharacterIndex.FromWire(0));
		Assert.Equal(CharacterIndex.Unknown, CharacterIndex.FromWire(CharacterIndex.Count + 1));
		Assert.Equal(CharacterIndex.Unknown, CharacterIndex.FromWire(-1));
	}

	[Fact]
	public void EachCharacterNamesThePrefabTheGameItselfLoads()
	{
		Assert.Equal("Special/Characters/Experiment", CharacterIndex.PrefabPath(0));
		Assert.Equal("Special/Characters/Milky", CharacterIndex.PrefabPath(1));
		Assert.Equal("Special/Characters/Dune", CharacterIndex.PrefabPath(2));
		Assert.Null(CharacterIndex.PrefabPath(CharacterIndex.Unknown));
		Assert.Null(CharacterIndex.PrefabPath(CharacterIndex.Count));
	}

	[Fact]
	public void TheBodyNameWinsAndTheStoredChoiceIsOnlyAFallback()
	{
		// The game instantiates the character's prefab, so the object is named after it (the local
		// player's root object); Unity's instantiate suffix is ignored.
		Assert.Equal(2, CharacterIndex.OfBody("Dune", 0));
		Assert.Equal(0, CharacterIndex.OfBody("Experiment", 2));
		Assert.Equal(1, CharacterIndex.OfBody("Milky (Clone)", 0));

		// A renamed body falls back to the choice the pre-run screen stored.
		Assert.Equal(1, CharacterIndex.OfBody("Character_1A2B", 1));
		Assert.Equal(CharacterIndex.Unknown, CharacterIndex.OfBody("Character_1A2B", CharacterIndex.Unknown));
		Assert.Equal(CharacterIndex.Unknown, CharacterIndex.OfBody(null, CharacterIndex.Count));
	}
}
