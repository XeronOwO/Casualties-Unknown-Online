using System.Linq;
using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The item-data verbs' grammar after the verb word: one target vocabulary for every verb, the
/// state argument as exactly one token, and each verb's own arguments with their refusals. Pure
/// — the console suites prove the wiring; this class pins what an operator can type.
/// </summary>
public class ItemDataVerbParserTests
{
	[Fact]
	public void Get_WithAnOptionalPath_Parses()
	{
		Assert.True(ItemDataVerbParser.TryParseGet(Tokens("id 5"), out var whole, out _));
		Assert.Equal(ItemDataQueryKind.InstanceId, whole.Target.Kind);
		Assert.True(whole.Path.IsRoot);

		Assert.True(ItemDataVerbParser.TryParseGet(Tokens("id 5 Liquids[0].Amount"), out var one, out _));
		Assert.Equal("Liquids[0].Amount", one.Path.Text);
	}

	[Fact]
	public void Get_WithATrailingToken_IsRefusedWithTheUsage()
	{
		Assert.False(ItemDataVerbParser.TryParseGet(Tokens("id 5 Condition extra"), out _, out var error));
		Assert.Contains("Usage:", error);
	}

	[Fact]
	public void Merge_RequiresOneCompound()
	{
		Assert.True(ItemDataVerbParser.TryParseMerge(Tokens("id 5 {Condition:0.5f}"), out var request, out _));
		Assert.Equal("Condition", request.State.Entries[0].Key);

		Assert.False(ItemDataVerbParser.TryParseMerge(Tokens("id 5 {Condition:0.5f} {Favourited:true}"), out _, out var extra));
		Assert.Contains("Usage:", extra);

		Assert.False(ItemDataVerbParser.TryParseMerge(Tokens("id 5 0.5"), out _, out var scalar));
		Assert.Contains("takes a compound", scalar);
	}

	[Fact]
	public void Merge_HasNoPathOfItsOwn()
	{
		Assert.False(ItemDataVerbParser.TryParseMerge(Tokens("id 5 Condition set 0.5f"), out _, out var error));
		Assert.Contains("Usage:", error);
	}

	[Fact]
	public void Modify_TakesAPathAnOperationAndAValue()
	{
		Assert.True(ItemDataVerbParser.TryParseModify(Tokens("id 5 Components.BatteryItem.charge set 0.9f"), out var request, out _));
		Assert.Equal("Components.BatteryItem.charge", request.Path.Text);
		Assert.Equal(ItemDataVerbParser.SetOperation, request.Operation);
		Assert.Equal(0.9f, (float)Assert.IsType<SnbtFloat>(request.Value).Value);
	}

	[Fact]
	public void Modify_Insert_TakesAnIndexBeforeTheValue()
	{
		Assert.True(ItemDataVerbParser.TryParseModify(Tokens("id 5 Liquids insert 1 {LiquidId:'water'}"), out var request, out _));
		Assert.Equal(ItemDataVerbParser.InsertOperation, request.Operation);
		Assert.Equal(1, request.Index);

		Assert.False(ItemDataVerbParser.TryParseModify(Tokens("id 5 Liquids insert x {LiquidId:'water'}"), out _, out var bad));
		Assert.Contains("insert needs a list index", bad);
	}

	[Theory]
	[InlineData("frobnicate")]
	[InlineData("value")]
	public void Modify_WithAnUnknownOperation_IsRefusedWithTheOperationList(string operation)
	{
		Assert.False(ItemDataVerbParser.TryParseModify(Tokens($"id 5 Condition {operation} 0.5f"), out _, out var error));
		Assert.Contains("is not a modify operation", error);
		Assert.Contains("set, merge, append, prepend", error);
	}

	[Fact]
	public void Remove_RequiresAPathAndEndsAfterIt()
	{
		Assert.True(ItemDataVerbParser.TryParseRemove(Tokens("id 5 Liquids[0]"), out var request, out _));
		Assert.Equal("Liquids[0]", request.Path.Text);

		Assert.False(ItemDataVerbParser.TryParseRemove(Tokens("id 5"), out _, out var missing));
		Assert.Contains("needs a path", missing);

		Assert.False(ItemDataVerbParser.TryParseRemove(Tokens("id 5 Liquids[0] extra"), out _, out var extra));
		Assert.Contains("Usage:", extra);
	}

	[Fact]
	public void MalformedSnbt_IsRefusedWithTheParserPosition()
	{
		Assert.False(ItemDataVerbParser.TryParseMerge(Tokens("id 5 {Condition:}"), out _, out var error));
		Assert.Contains("The state is not SNBT:", error);
		Assert.Contains("at ", error);
	}

	/// <summary>The cursor target is one vocabulary: the query parser owns it and every write verb parses through it.</summary>
	[Fact]
	public void CursorTarget_IsAdmittedByEveryWriteVerb()
	{
		Assert.True(ItemDataVerbParser.TryParseModify(Tokens("cursor Condition set 0.5f"), out var modify, out _));
		Assert.Equal(ItemDataQueryKind.Cursor, modify.Target.Kind);

		Assert.True(ItemDataVerbParser.TryParseMerge(Tokens("cursor {Condition:0.5f}"), out var merge, out _));
		Assert.Equal(ItemDataQueryKind.Cursor, merge.Target.Kind);

		Assert.True(ItemDataVerbParser.TryParseRemove(Tokens("cursor Liquids[0]"), out var remove, out _));
		Assert.Equal(ItemDataQueryKind.Cursor, remove.Target.Kind);
	}

	private static string[] Tokens(string text) =>
		[.. CommandLineTokenizer.Tokenize(text).Select(token => token.Unquoted)];
}
