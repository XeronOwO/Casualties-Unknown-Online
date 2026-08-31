using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class CommandLineTokenizerTests
{
	[Fact]
	public void Tokenize_RespectsDoubleQuotedSpaces()
	{
		var tokens = CommandLineTokenizer.Tokenize("/kick \"John Doe\" extra");

		Assert.Equal(3, tokens.Count);
		Assert.Equal("John Doe", tokens[1].Unquoted);
		Assert.Equal("extra", tokens[2].Unquoted);
	}

	[Fact]
	public void Tokenize_KeepsBraceGroupAsSingleToken()
	{
		var tokens = CommandLineTokenizer.Tokenize("/data {\"name\": \"John Doe\", \"value\": 1}");

		Assert.Equal(2, tokens.Count);
		Assert.Equal("{\"name\": \"John Doe\", \"value\": 1}", tokens[1].Text);
	}

	[Fact]
	public void CurrentToken_ReturnsEmptyTokenAfterTrailingWhitespace()
	{
		var current = CommandLineTokenizer.CurrentToken("/kick ");

		Assert.Equal(0, current.Length);
		Assert.Equal("/kick ".Length, current.Start);
	}

	[Fact]
	public void TokenAtCursor_ReturnsTokenUnderCursor()
	{
		var token = CommandLineTokenizer.TokenAtCursor("/kick Jo", 7);

		Assert.Equal("Jo", token.Text);
		Assert.Equal(6, token.Start);
	}

	[Fact]
	public void TokenAtCursor_ReturnsEmptyAtWhitespace()
	{
		var token = CommandLineTokenizer.TokenAtCursor("/kick Jo", 5);

		Assert.Equal(0, token.Length);
		Assert.Equal(5, token.Start);
	}

	[Fact]
	public void QuoteIfNeeded_QuotesValuesWithSpaces() =>
		Assert.Equal("\"John Doe\"", CommandLineTokenizer.QuoteIfNeeded("John Doe"));

	[Fact]
	public void QuoteIfNeeded_LeavesSimpleValuesUntouched() =>
		Assert.Equal("kick", CommandLineTokenizer.QuoteIfNeeded("kick"));

	[Fact]
	public void Unquote_StripsMatchingQuotes()
	{
		var token = CommandLineTokenizer.Tokenize("\"John Doe\"")[0];

		Assert.Equal("John Doe", token.Unquoted);
	}
}
