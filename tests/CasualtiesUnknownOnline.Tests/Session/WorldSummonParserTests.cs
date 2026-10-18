using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The <c>/summon</c> grammar — Minecraft's summon template mapped onto the game's identifiers: the type word
/// (<c>item</c> or a bare prefab id), the native console's position words (or a literal point), and the item's
/// state document as its own trailing argument. Pure on purpose — whether the build knows the prefab, whether a
/// state binds to it and whether the created object can report are the creation seam's answers, so what this class
/// pins is only what a player can type.
/// </summary>
public class WorldSummonParserTests
{
	[Fact]
	public void PrefabAlone_IsAnEntityAtThePointerWithNoState()
	{
		Assert.True(WorldSummonParser.TryParse(["beartrap"], out var request, out var error));

		Assert.Null(error);
		Assert.Equal("beartrap", request.PrefabId);
		Assert.Equal(WorldSummonKind.Entity, request.Kind);
		Assert.Equal(WorldSummonAnchor.Cursor, request.Position.Anchor);
		Assert.Empty(request.StateText);
	}

	[Theory]
	[InlineData("cursor", WorldSummonAnchor.Cursor)]
	[InlineData("player", WorldSummonAnchor.Player)]
	[InlineData("random", WorldSummonAnchor.Random)]
	[InlineData("PLAYER", WorldSummonAnchor.Player)]
	public void NativePositionWords_AreAcceptedCaseInsensitively(string token, WorldSummonAnchor expected)
	{
		Assert.True(WorldSummonParser.TryParse(["spider", token], out var request, out var error));

		Assert.Null(error);
		Assert.Equal(expected, request.Position.Anchor);
		Assert.Empty(request.StateText);
	}

	[Fact]
	public void LiteralPoint_IsParsedAsItsOwnPosition()
	{
		Assert.True(WorldSummonParser.TryParse(["medcrate", "12.5,-3"], out var request, out var error));

		Assert.Null(error);
		Assert.Null(request.Position.Anchor); // a typed point is not a keyword
		Assert.Equal(12.5f, request.Position.X);
		Assert.Equal(-3f, request.Position.Y);
	}

	[Theory]
	[InlineData("item", "bandage")]
	[InlineData("ITEM", "bandage")]
	public void ItemWord_NamesALooseItemEntity(string word, string definition)
	{
		Assert.True(WorldSummonParser.TryParse([word, definition], out var request, out var error));

		Assert.Null(error);
		Assert.Equal(WorldSummonKind.Item, request.Kind);
		Assert.Equal("bandage", request.PrefabId);
	}

	[Fact]
	public void ItemWord_WithAPositionAndAState()
	{
		Assert.True(WorldSummonParser.TryParse(
			["item", "waterbottle", "player", "{Liquids:[{LiquidId:'water',Amount:500}],Condition:0.8}"],
			out var request,
			out var error));

		Assert.Null(error);
		Assert.Equal(WorldSummonKind.Item, request.Kind);
		Assert.Equal(WorldSummonAnchor.Player, request.Position.Anchor);
		Assert.Contains("Liquids:[{LiquidId:", request.StateText);
		Assert.Contains("Amount:500", request.StateText);
	}

	[Fact]
	public void ItemWordWithoutADefinition_IsRefusedWithTheShape()
	{
		Assert.False(WorldSummonParser.TryParse(["item"], out _, out var error));

		Assert.Contains("/summon item <itemId>", error);
		Assert.Contains("Usage:", error);
	}

	[Fact]
	public void StateWithoutAPosition_IsTheStateAndThePositionStaysTheDefault()
	{
		Assert.True(WorldSummonParser.TryParse(["item", "bandage", "{Condition:0.5f}"], out var request, out var error));

		Assert.Null(error);
		Assert.Equal(WorldSummonAnchor.Cursor, request.Position.Anchor);
		Assert.Equal("{Condition:0.5f}", request.StateText);
	}

	[Fact]
	public void StateAttachedToTheId_IsRefusedWithTheAcceptedShape()
	{
		Assert.False(WorldSummonParser.TryParse(["bandage{Condition:0.5f}"], out _, out var error));

		Assert.Contains("The state is its own argument", error);
		// The correction names the form instead of echoing the rejected line, and it does not assume the family:
		// a bare item id needs the type word too, which the example says.
		Assert.Contains("/summon <prefabId> [<position>] {<key>:<value>}", error);
		Assert.DoesNotContain("/summon bandage{Condition:0.5f}", error);
		Assert.Contains("an item definition also needs the item word", error);
		Assert.Contains("Usage:", error);
	}

	/// <summary>The literal point must be finite: a NaN or an infinity parsed from text would travel as a "position" and poison everything written from it.</summary>
	[Theory]
	[InlineData("NaN,1")]
	[InlineData("1,Infinity")]
	[InlineData("-Infinity,-Infinity")]
	public void NonFiniteLiteralPoint_IsRefused(string point)
	{
		Assert.False(WorldSummonParser.TryParse(["beartrap", point], out _, out var error));

		Assert.Contains("is not a position", error);
		Assert.Contains("cursor | player | random | <x>,<y>", error);
	}

	[Fact]
	public void UnknownPosition_IsRefusedWithTheVocabulary()
	{
		Assert.False(WorldSummonParser.TryParse(["beartrap", "nowhere"], out _, out var error));

		Assert.Contains("'nowhere' is not a position", error);
		Assert.Contains("cursor | player | random | <x>,<y>", error);
	}

	[Theory]
	[InlineData("beartrap x,1")]
	[InlineData("beartrap 1,")]
	[InlineData("beartrap ,2")]
	[InlineData("beartrap player player")]
	public void MalformedPosition_IsRefused(string line)
	{
		Assert.False(WorldSummonParser.TryParse(line.Split(' '), out _, out var error));
		Assert.NotNull(error);
	}

	[Fact]
	public void AMalformedState_IsTheStateParsersOwnSentence()
	{
		Assert.False(WorldSummonParser.TryParse(["item", "bandage", "player", "{Condition:}"], out _, out var error));

		Assert.Contains("SNBT", error);
	}

	/// <summary>The state is a compound, never a bare value or a list: that is the shape the item's document is.</summary>
	[Fact]
	public void ANonCompoundState_IsRefusedByName()
	{
		Assert.False(WorldSummonParser.TryParse(["item", "bandage", "player", "[1,2]"], out _, out var error));

		Assert.Contains("is a compound", error);
	}

	[Theory]
	[InlineData(new string[0], "Usage:")]
	[InlineData(new[] { "{Condition:0.5f}" }, "The state is its own argument")]
	public void MalformedSummon_IsRefusedWithItsOwnSentence(string[] tokens, string expected)
	{
		Assert.False(WorldSummonParser.TryParse(tokens, out _, out var error));

		Assert.Contains(expected, error);
	}

	[Fact]
	public void ExtraTokens_AreRefusedWithTheUsage()
	{
		Assert.False(WorldSummonParser.TryParse(["item", "bandage", "player", "{Condition:0.5f}", "extra"], out _, out var error));

		Assert.Equal($"Usage: {WorldSummonParser.Usage}", error);
	}
}
