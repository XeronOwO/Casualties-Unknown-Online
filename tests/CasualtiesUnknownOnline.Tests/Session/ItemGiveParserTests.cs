using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The <c>/give</c> grammar: the one body, the definition, the destination slot and
/// the property tokens that follow. Pure on purpose — whether the build knows the
/// definition and whether the values apply are answered where the item is created,
/// so what this class pins is only what a player can type.
/// </summary>
public class ItemGiveParserTests
{
	[Fact]
	public void SelectorAndDefinition_AreParsed()
	{
		Assert.True(ItemGiveParser.TryParse(["@p", "bandage"], out var request, out var error));

		Assert.Null(error);
		Assert.Equal("@p", request.Selector);
		Assert.Equal("bandage", request.DefinitionId);
		Assert.Equal(-1, request.Slot);
		Assert.Empty(request.PropertyTokens);
	}

	[Fact]
	public void NoSelector_MeansTheCallersOwnBody()
	{
		Assert.True(ItemGiveParser.TryParse(["bandage"], out var request, out _));

		Assert.Null(request.Selector);
		Assert.Equal("bandage", request.DefinitionId);
	}

	/// <summary>
	/// The one shape that could be read two ways: the read/write verbs spell
	/// "<c>selector slot n</c>" for the item IN that slot, while a give spells the
	/// SAME words for the slot the created item lands in. A give that read it the
	/// other way would refuse a definition of "bandage" and never create anything.
	/// </summary>
	[Fact]
	public void SlotAfterTheDefinition_IsTheDestinationSlot()
	{
		Assert.True(ItemGiveParser.TryParse(["@p", "bandage", "slot", "2"], out var request, out _));

		Assert.Equal("@p", request.Selector);
		Assert.Equal("bandage", request.DefinitionId);
		Assert.Equal(2, request.Slot);
		Assert.Empty(request.PropertyTokens);
	}

	[Fact]
	public void Properties_AreCarriedAsTokensForTheSideThatCreatesTheItem()
	{
		Assert.True(ItemGiveParser.TryParse(["@p", "waterbottle", "slot", "1", "liquid", "water", "500"], out var request, out _));

		Assert.Equal(1, request.Slot);
		Assert.Equal(["liquid", "water", "500"], request.PropertyTokens);
	}

	[Fact]
	public void Properties_ParseThroughTheWriteVerbsOwnGrammar()
	{
		Assert.True(ItemGiveParser.TryParseProperties(["liquid", "water", "500", "condition", "0.5"], out var writes, out var error));

		Assert.Null(error);
		Assert.Equal(2, writes.Count);
		Assert.Equal(ItemDataProperty.Liquid, writes[0].Property);
		Assert.Equal("water", writes[0].Name);
		Assert.Equal(500f, writes[0].Number);
		Assert.Equal(ItemDataProperty.Condition, writes[1].Property);
		Assert.Equal(0.5f, writes[1].Number);
	}

	/// <summary>The item-target words name an ITEM on the other verbs; a give names the body that receives it, so the word is answered rather than read as a definition id.</summary>
	[Theory]
	[InlineData("id")]
	[InlineData("slot")]
	[InlineData("cursor")]
	public void ItemTargetKeyword_IsRefusedByName(string keyword)
	{
		Assert.False(ItemGiveParser.TryParse([keyword, "5", "bandage"], out _, out var error));
		Assert.Contains("names an item, not a definition", error);
	}

	/// <summary>
	/// The wrong order is the one shape that could be swallowed: <c>@p slot 2 bandage</c>
	/// spells the read grammar's "item in slot 2" if the selector parser is allowed to
	/// consume two tokens, and the operator's destination slot would vanish silently.
	/// It is refused by name instead.
	/// </summary>
	[Fact]
	public void SlotBeforeTheDefinition_IsRefusedRatherThanDropped()
	{
		Assert.False(ItemGiveParser.TryParse(["@p", "slot", "2", "bandage"], out _, out var error));
		Assert.Contains("names an item, not a definition", error);
		Assert.Contains("/give @p bandage slot 2", error);
	}

	[Fact]
	public void NoDefinition_IsRefusedWithTheUsage()
	{
		Assert.False(ItemGiveParser.TryParse(["@p"], out _, out var error));
		Assert.Contains("Usage:", error);
		Assert.Contains("/give", error);
	}

	[Fact]
	public void EmptyLine_IsRefusedWithTheUsage()
	{
		Assert.False(ItemGiveParser.TryParse([], out _, out var error));
		Assert.Contains("Usage:", error);
	}

	[Theory]
	[InlineData("x")]
	[InlineData("-1")]
	public void BadDestinationSlot_IsRefused(string slot)
	{
		Assert.False(ItemGiveParser.TryParse(["@p", "bandage", "slot", slot], out _, out var error));
		Assert.Contains("slot", error);
	}

	/// <summary>The property vocabulary is the write verb's, and its refusal must quote the GIVE usage rather than telling the operator to type a /data line they cannot run.</summary>
	[Fact]
	public void UnknownProperty_IsRefusedWithTheGiveUsage()
	{
		Assert.False(ItemGiveParser.TryParseProperties(["quality", "5"], out _, out var error));
		Assert.Contains("not a property to write", error);
		Assert.Contains(ItemGiveParser.Usage, error);
		Assert.DoesNotContain(ItemDataWriteParser.Usage, error);
	}
}
