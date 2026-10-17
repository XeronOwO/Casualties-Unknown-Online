using CasualtiesUnknownOnline.Runtime.Data;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The item state language's number grammar: Minecraft's literal forms resolved to this
/// language's integer/float kinds (suffixes, base prefixes, digit groups, signedness), and the
/// refusals that keep a value the adapter cannot bind out of the tree.
/// </summary>
public class SnbtNumberTests
{
	[Theory]
	[InlineData("{N:1b}", "{N:1}")]
	[InlineData("{N:1s}", "{N:1}")]
	[InlineData("{N:1i}", "{N:1}")]
	[InlineData("{N:1L}", "{N:1}")]
	[InlineData("{N:0x1F}", "{N:31}")]
	[InlineData("{N:0b101}", "{N:5}")]
	[InlineData("{N:1_000}", "{N:1000}")]
	[InlineData("{N:-16sb}", "{N:-16}")]
	[InlineData("{N:240ub}", "{N:-16}")]
	[InlineData("{N:1.5e2f}", "{N:150f}")]
	[InlineData("{N:0x11ub}", "{N:17}")]
	[InlineData("{N:-9223372036854775808L}", "{N:-9223372036854775808L}")]
	[InlineData("{N:-0.5d}", "{N:-0.5d}")]
	public void NumberLiterals_ResolveToTheirKindAndValue(string text, string canonical)
	{
		Assert.True(SnbtParser.TryParse(text, out var value, out var error), error);
		Assert.Equal(canonical, SnbtWriter.Write(value));
	}

	[Theory]
	[InlineData("{N:01x}", "does not end in a number suffix")]
	[InlineData("{N:1e999}", "finite")]
	[InlineData("{N:300b}", "does not fit")]
	[InlineData("{N:1_}", "underscore")]
	[InlineData("{N:3000000000}", "does not fit a 32-bit int")]
	public void BadNumbers_AreRefusedWithTheirReason(string text, string expected)
	{
		Assert.False(SnbtParser.TryParse(text, out _, out var error));
		Assert.Contains(expected, error);
	}
}
