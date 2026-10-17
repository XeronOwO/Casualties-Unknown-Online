using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The <c>/give</c> grammar: the one body, the definition with its attached SNBT state, and
/// the destination slot. Pure on purpose — whether the build knows the definition and whether
/// the state binds are answered where the item is created, so what this class pins is only
/// what a player can type.
/// </summary>
public class ItemGiveParserTests
{
	private const string Q = "\"";

	[Fact]
	public void SelectorAndDefinition_AreParsed()
	{
		Assert.True(ItemGiveParser.TryParse(["@p", "bandage"], out var request, out var error));

		Assert.Null(error);
		Assert.Equal("@p", request.Selector);
		Assert.Equal("bandage", request.DefinitionId);
		Assert.Equal(-1, request.Slot);
		Assert.Empty(request.StateText);
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
	/// "<c>selector slot n</c>" for the item IN that slot, while a give spells the SAME words for
	/// the slot the created item lands in. A give that read it the other way would refuse a
	/// definition of "bandage" and never create anything.
	/// </summary>
	[Fact]
	public void SlotAfterTheDefinition_IsTheDestinationSlot()
	{
		Assert.True(ItemGiveParser.TryParse(["@p", "bandage", "slot", "2"], out var request, out _));

		Assert.Equal("@p", request.Selector);
		Assert.Equal("bandage", request.DefinitionId);
		Assert.Equal(2, request.Slot);
		Assert.Empty(request.StateText);
	}

	[Fact]
	public void StateAttachedToTheItemId_IsParsedIntoTheCanonicalText()
	{
		Assert.True(ItemGiveParser.TryParse(
			["@p", "waterbottle{Liquids:[{LiquidId:'water',Amount:500}],Condition:0.8}", "slot", "1"],
			out var request,
			out var error));

		Assert.Null(error);
		Assert.Equal("waterbottle", request.DefinitionId);
		Assert.Equal(1, request.Slot);
		Assert.NotEmpty(request.StateText);
		Assert.Equal(
			"{Liquids:[{LiquidId:" + Q + "water" + Q + ",Amount:500}],Condition:0.8d}",
			request.StateText);
	}

	/// <summary>The console tokenizer keeps a braced group together, so the state may contain spaces and still be ONE token.</summary>
	[Fact]
	public void StateWithSpaces_IsStillOneArgument()
	{
		Assert.True(ItemGiveParser.TryParse(["@p", "largebattery{Components: {BatteryItem: {charge: 0.9, on: true}}}"], out var request, out _));

		Assert.Equal("largebattery", request.DefinitionId);
		Assert.NotEmpty(request.StateText);
		Assert.Contains("Components:{BatteryItem:{charge:0.9d,on:true}}", request.StateText);
	}

	[Fact]
	public void StateSeparatedFromTheItemId_IsRefusedWithTheUsage()
	{
		Assert.False(ItemGiveParser.TryParse(["@p", "bandage", "{Condition:0.5}"], out _, out var error));
		Assert.Contains("Usage:", error);
	}

	[Fact]
	public void StateThatIsNotACompound_IsRefusedByName()
	{
		// The attached form always begins with '{', so a non-compound state reaches the parser
		// through the WIRE text (a member's request); the attached form's own bracket mistake is
		// answered as a separator mistake instead.
		Assert.False(ItemGiveParser.TryParseState("[1,2]", out _, out _, out var error));
		Assert.Contains("is a compound", error);

		Assert.False(ItemGiveParser.TryParse(["@p", "bandage[1,2]"], out _, out var attached));
		Assert.Contains("attaches to the item id with '{'", attached);
	}

	[Fact]
	public void MalformedState_IsRefusedWithTheParserPosition()
	{
		Assert.False(ItemGiveParser.TryParse(["@p", "bandage{Condition:}"], out _, out var error));
		Assert.Contains("The state is not SNBT:", error);
		Assert.Contains("at ", error);
	}

	[Fact]
	public void StateLongerThanTheWireBound_IsRefused()
	{
		var text = "{Condition:0.5," + new string('x', ItemGiveParser.MaxStateLength) + "}";
		Assert.False(ItemGiveParser.TryParse(["@p", "bandage" + text], out _, out var error));
		Assert.Contains("at most", error);
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
	/// The wrong order is the one shape that could be swallowed: <c>@p slot 2 bandage</c> spells
	/// the read grammar's "item in slot 2" if the selector parser is allowed to consume two
	/// tokens, and the operator's destination slot would vanish silently. It is refused by name
	/// instead.
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

	[Fact]
	public void ATokenAfterTheState_IsRefusedWithTheUsage()
	{
		Assert.False(ItemGiveParser.TryParse(["@p", "bandage{Condition:0.5}", "5"], out _, out var error));
		Assert.Contains("Usage:", error);
	}

	[Fact]
	public void TryParseState_AcceptsTheWireTextAndRejectsAGarbageList()
	{
		Assert.True(ItemGiveParser.TryParseState("{Condition:0.5}", out var state, out var canonical, out _));
		Assert.NotNull(state);
		Assert.Equal("{Condition:0.5d}", canonical);

		Assert.True(ItemGiveParser.TryParseState("", out var none, out var empty, out _));
		Assert.Null(none);
		Assert.Empty(empty);

		Assert.False(ItemGiveParser.TryParseState("[1,2]", out _, out _, out var error));
		Assert.Contains("is a compound", error);
	}
}
