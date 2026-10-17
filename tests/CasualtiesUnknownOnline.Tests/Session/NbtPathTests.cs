using CasualtiesUnknownOnline.Runtime.Data;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The path language and the tree edits over it: keys, quoted keys, indices (negative
/// included), the five modify operations with their refusals, and the recursive overlay the
/// give verb's state uses. Pure — no command, no adapter — because these are the rules every
/// write verb inherits.
/// </summary>
public class NbtPathTests
{
	[Fact]
	public void KeysAndIndices_Parse()
	{
		Assert.True(NbtPathParser.TryParse("Liquids[0].Amount", out var path, out _));
		Assert.Equal("Liquids[0].Amount", path.Text);

		Assert.True(NbtPathParser.TryParse("Components.'A key'", out var quoted, out _));
		Assert.Equal("Components.\"A key\"", quoted.Text);

		Assert.True(NbtPathParser.TryParse("Liquids.[-1].Amount", out var negative, out _));
		Assert.Equal("Liquids[-1].Amount", negative.Text);

		Assert.True(NbtPathParser.TryParse("", out var root, out _));
		Assert.True(root.IsRoot);
	}

	[Theory]
	[InlineData("Liquids[]", "wildcard")]
	[InlineData("Liquids[{LiquidId:'water'}]", "filter")]
	[InlineData("[0]", "starts with a key")]
	[InlineData("A.", "ends with a dot")]
	[InlineData("A..B", "expected a key")]
	[InlineData("A[1", "expected ']'")]
	[InlineData("A[x]", "whole number")]
	[InlineData("''", "empty key")]
	public void BadPaths_AreRefusedWithTheirReason(string text, string expected)
	{
		Assert.False(NbtPathParser.TryParse(text, out _, out var error));
		Assert.Contains(expected, error);
	}

	[Fact]
	public void Resolve_ReadsAKeyAQuotedKeyAndANegativeIndex()
	{
		var document = Parse("{Liquids:[{LiquidId:'water',Amount:500f},{LiquidId:'blood',Amount:100f}],Components:{'Battery Item':{charge:0.5}}}");

		Assert.True(Resolve(document, "Liquids[0].Amount", out var first));
		Assert.Equal("500f", SnbtWriter.Write(first));

		Assert.True(Resolve(document, "Liquids[-1].LiquidId", out var last));
		Assert.Equal("\"blood\"", SnbtWriter.Write(last));

		Assert.True(Resolve(document, "Components.'Battery Item'.charge", out var charge));
		Assert.Equal("0.5d", SnbtWriter.Write(charge));
	}

	[Theory]
	[InlineData("Liquids[3]", "out of range")]
	[InlineData("Components.Nope", "has no 'Nope'")]
	[InlineData("Condition.Amount", "not a compound")]
	public void Resolve_AMissingPath_IsRefusedByName(string path, string expected)
	{
		var document = Parse("{Condition:1,Liquids:[{LiquidId:'water',Amount:500}],Components:{Battery:{charge:0.5}}}");
		var parsed = ParsePath(path);

		Assert.False(SnbtTree.TryResolve(document, parsed, out _, out var error));
		Assert.Contains(expected, error);
	}

	[Fact]
	public void Set_ReplacesAnExistingValueAndDoesNotCreateAKey()
	{
		var document = Parse("{Condition:1,Liquids:[]}");

		Assert.True(SnbtTree.TrySet(document, ParsePath("Condition"), new SnbtFloat(0.5f, Single: true), out var updated, out _));
		Assert.Equal("{Condition:0.5f,Liquids:[]}", SnbtWriter.Write(updated));

		Assert.False(SnbtTree.TrySet(document, ParsePath("Components"), SnbtCompound.Empty, out _, out var error));
		Assert.Contains("has no 'Components'", error);
	}

	[Fact]
	public void Merge_IsShallowAtThePath()
	{
		var document = Parse("{Components:{Battery:{charge:0.5,on:false}}}");

		Assert.True(SnbtTree.TryMerge(document, ParsePath("Components"), (SnbtCompound)Parse("{Battery:{charge:0.9}}"), out var updated, out _));
		Assert.Equal("{Components:{Battery:{charge:0.9d}}}", SnbtWriter.Write(updated));

		Assert.False(SnbtTree.TryMerge(document, ParsePath("Components.Battery.charge"), (SnbtCompound)Parse("{x:1}"), out _, out var error));
		Assert.Contains("not a compound", error);
	}

	[Fact]
	public void Insert_AppendsPrependsAndInsertsAtIndex()
	{
		var document = Parse("{Liquids:[{LiquidId:'water'}]}");
		var blood = Parse("{LiquidId:'blood'}");

		Assert.True(SnbtTree.TryInsert(document, ParsePath("Liquids"), 0, blood, out var prepended, out _));
		Assert.Equal(Canonical("{Liquids:[{LiquidId:'blood'},{LiquidId:'water'}]}"), SnbtWriter.Write(prepended));

		Assert.True(SnbtTree.TryInsert(document, ParsePath("Liquids"), 1, blood, out var appended, out _));
		Assert.Equal(Canonical("{Liquids:[{LiquidId:'water'},{LiquidId:'blood'}]}"), SnbtWriter.Write(appended));

		Assert.True(SnbtTree.TryInsert(document, ParsePath("Liquids"), 1, blood, out var inserted, out _));
		Assert.Equal(Canonical("{Liquids:[{LiquidId:'water'},{LiquidId:'blood'}]}"), SnbtWriter.Write(inserted));
	}

	[Fact]
	public void Insert_InTheMiddle_KeepsTheOrder()
	{
		var document = Parse("{L:[1,2,3]}");

		Assert.True(SnbtTree.TryInsert(document, ParsePath("L"), 1, new SnbtInteger(9), out var inserted, out _));

		Assert.Equal("{L:[1,9,2,3]}", SnbtWriter.Write(inserted));
	}

	[Theory]
	[InlineData(2, "out of range")]
	[InlineData(-1, "out of range")]
	public void Insert_OutOfRange_IsRefused(int index, string expected)
	{
		var document = Parse("{Liquids:[]}");

		Assert.False(SnbtTree.TryInsert(document, ParsePath("Liquids"), index, SnbtCompound.Empty, out _, out var error));
		Assert.Contains(expected, error);
	}

	[Fact]
	public void Insert_OnANonList_IsRefused()
	{
		Assert.False(SnbtTree.TryInsert(Parse("{Condition:1}"), ParsePath("Condition"), 0, new SnbtInteger(1), out _, out var error));
		Assert.Contains("not a list", error);
	}

	[Fact]
	public void Remove_DeletesAKeyOrAnElementAndNamesWhatWent()
	{
		var document = Parse("{Condition:1,Liquids:[{LiquidId:'water'},{LiquidId:'blood'}]}");

		Assert.True(SnbtTree.TryRemove(document, ParsePath("Liquids[0]"), out var updated, out var removed, out _));
		Assert.Equal(Canonical("{Condition:1,Liquids:[{LiquidId:'blood'}]}"), SnbtWriter.Write(updated));
		Assert.Equal(Canonical("{LiquidId:'water'}"), SnbtWriter.Write(removed));

		Assert.True(SnbtTree.TryRemove(document, ParsePath("Condition"), out var noCondition, out _, out _));
		Assert.Equal(Canonical("{Liquids:[{LiquidId:'water'},{LiquidId:'blood'}]}"), SnbtWriter.Write(noCondition));
	}

	[Fact]
	public void Remove_TheRoot_IsRefused()
	{
		Assert.False(SnbtTree.TryRemove(Parse("{Condition:1}"), NbtPath.Root, out _, out _, out var error));
		Assert.Contains("whole state", error);
	}

	[Fact]
	public void Overlay_MergesCompoundsRecursivelyAndReplacesEverythingElse()
	{
		var target = Parse("{Condition:1,Components:{Battery:{charge:0.5,on:false}},Liquids:[]}");
		var patch = Parse("{Components:{Battery:{charge:0.9}},Liquids:[{LiquidId:'water',Amount:500}]}");

		Assert.Equal(
			Canonical("{Condition:1,Components:{Battery:{charge:0.9d,on:false}},Liquids:[{LiquidId:'water',Amount:500}]}"),
			SnbtWriter.Write(SnbtTree.Overlay(target, patch)));
	}

	/// <summary>The canonical form of one single-quoted SNBT text — the writer's own output, so a test never spells a quoted value by hand.</summary>
	private static string Canonical(string text) => SnbtWriter.Write(Parse(text));

	private static SnbtValue Parse(string text)
	{
		Assert.True(SnbtParser.TryParse(text, out var value, out var error), error);
		return value;
	}

	private static NbtPath ParsePath(string text)
	{
		Assert.True(NbtPathParser.TryParse(text, out var path, out var error), error);
		return path;
	}

	private static bool Resolve(SnbtValue document, string path, out SnbtValue value) =>
		SnbtTree.TryResolve(document, ParsePath(path), out value, out _);
}
