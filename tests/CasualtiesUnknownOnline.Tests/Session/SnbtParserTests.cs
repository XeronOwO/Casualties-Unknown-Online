using CasualtiesUnknownOnline.Runtime.Data;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The item state language's text: the SNBT subset the commands accept (Minecraft's current
/// literals, escapes, heterogeneous lists and trailing commas), the canonical form the writer
/// prints, and every refusal the dialect promises — typed arrays, NBT operations, duplicate
/// keys and the depth bound included.
/// </summary>
public class SnbtParserTests
{
	private const string Q = "\"";

	[Theory]
	[InlineData("{Condition:0.8}", "{Condition:0.8d}")]
	[InlineData("{Condition:0.8f}", "{Condition:0.8f}")]
	[InlineData("{Amount:500}", "{Amount:500}")]
	[InlineData("{Lit:true,Off:false}", "{Lit:true,Off:false}")]
	[InlineData("{Name:'water'}", "{Name:" + Q + "water" + Q + "}")]
	[InlineData("{Empty:{},List:[]}", "{Empty:{},List:[]}")]
	[InlineData("{A:[1,'two',true]}", "{A:[1," + Q + "two" + Q + ",true]}")]
	[InlineData("{A:[1,2,],}", "{A:[1,2]}")]
	[InlineData("{A: 1 , B : 2}", "{A:1,B:2}")]
	public void Parse_ThenWrite_IsTheCanonicalForm(string text, string canonical)
	{
		Assert.True(SnbtParser.TryParse(text, out var value, out var error), error);
		Assert.Equal(canonical, SnbtWriter.Write(value));
	}

	[Fact]
	public void Escapes_AreTheOnesTheLanguageKnows()
	{
		Assert.True(SnbtParser.TryParse("{T:'a\\'b\\\\c\\nd\\te\\u0041\\x42'}", out var value, out var error), error);
		var text = SnbtWriter.Write(value);

		Assert.Contains("'b\\\\c\\nd\\teAB", text);
	}

	[Fact]
	public void UnquotedStrings_AreValuesToo()
	{
		Assert.True(SnbtParser.TryParse("{LiquidId:water}", out var value, out var error), error);
		Assert.Equal("{LiquidId:" + Q + "water" + Q + "}", SnbtWriter.Write(value));
	}

	[Theory]
	[InlineData("[B;1b,2b]")]
	[InlineData("[I;1,2]")]
	[InlineData("[L;1L,2L]")]
	public void TypedArrays_AreRefusedByName(string text)
	{
		Assert.False(SnbtParser.TryParse(text, out _, out var error));
		Assert.Contains("typed array", error);
	}

	[Theory]
	[InlineData("{A:bool(1)}")]
	[InlineData("{A:uuid('f81d4fae-7dec-11d0-a765-00a0c91e6bf6')}")]
	public void NbtOperations_AreRefusedByName(string text)
	{
		Assert.False(SnbtParser.TryParse(text, out _, out var error));
		Assert.Contains("operation", error);
	}

	[Theory]
	[InlineData("{A:1,A:2}", "appears twice")]
	[InlineData("{A:1", "end of the text")]
	[InlineData("{A}", "expected ':'")]
	[InlineData("{'':1}", "empty key")]
	[InlineData("{A:1}x", "expected the end")]
	[InlineData("{T:'unclosed}", "does not close")]
	[InlineData("{T:'\\q'}", "not an escape")]
	[InlineData("{T:'\\N{Snowman}'}", "named escape")]
	[InlineData("{T:'\\U0000D800'}", "Unicode code point")]
	public void BadText_IsRefusedWithItsReason(string text, string expected)
	{
		Assert.False(SnbtParser.TryParse(text, out _, out var error));
		Assert.Contains(expected, error);
	}

	[Fact]
	public void ADeeplyNestedValue_IsRefusedAtTheDepthBound()
	{
		var text = new string('[', SnbtParser.MaxDepth + 2) + new string(']', SnbtParser.MaxDepth + 2);

		Assert.False(SnbtParser.TryParse(text, out _, out var error));
		Assert.Contains("nests deeper", error);
	}

	[Fact]
	public void AKeyThatNeedsQuoting_IsWrittenQuoted()
	{
		Assert.True(SnbtParser.TryParse("{'a b':1}", out var value, out _));
		Assert.Equal("{" + Q + "a b" + Q + ":1}", SnbtWriter.Write(value));

		Assert.True(SnbtParser.TryParse("{1:1}", out var numericKey, out _));
		Assert.Equal("{" + Q + "1" + Q + ":1}", SnbtWriter.Write(numericKey));
	}
}
