using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class HostRulesJsonParserTests
{
	[Fact]
	public void Parse_FlatObject_ReturnsProperties()
	{
		var ok = HostRulesJsonParser.TryParse(
			"{ \"AllowLateJoin\": false, \"PiggybackWeightMultiplier\": 0.8 }",
			out var values,
			out var error);

		Assert.True(ok);
		Assert.Null(error);
		Assert.Equal("false", values["AllowLateJoin"]);
		Assert.Equal("0.8", values["PiggybackWeightMultiplier"]);
	}

	[Fact]
	public void Parse_EmptyObject_ReturnsEmptyAndSuccess()
	{
		var ok = HostRulesJsonParser.TryParse("{}", out var values, out var error);

		Assert.True(ok);
		Assert.Null(error);
		Assert.Empty(values);
	}

	[Fact]
	public void Parse_PropertyNamesAreCaseInsensitive()
	{
		var ok = HostRulesJsonParser.TryParse("{\"allowlatejoin\": true}", out var values, out var error);

		Assert.True(ok);
		Assert.Null(error);
		Assert.Equal("true", values["AllowLateJoin"]);
	}

	[Fact]
	public void Parse_QuotedStringValue_ReturnsUnquoted()
	{
		var ok = HostRulesJsonParser.TryParse("{\"name\": \"value\"}", out var values, out var error);

		Assert.True(ok);
		Assert.Null(error);
		Assert.Equal("value", values["name"]);
	}

	[Fact]
	public void Parse_MissingColon_ReturnsError()
	{
		var ok = HostRulesJsonParser.TryParse("{\"AllowLateJoin\" false}", out _, out var error);

		Assert.False(ok);
		Assert.NotNull(error);
	}

	[Fact]
	public void Parse_MissingObjectEnd_ReturnsError()
	{
		var ok = HostRulesJsonParser.TryParse("{\"AllowLateJoin\": false", out _, out var error);

		Assert.False(ok);
		Assert.NotNull(error);
	}

	[Fact]
	public void Parse_EmptyOrNull_ReturnsError()
	{
		Assert.False(HostRulesJsonParser.TryParse("", out _, out var error1));
		Assert.NotNull(error1);
		Assert.False(HostRulesJsonParser.TryParse(null!, out _, out var error2));
		Assert.NotNull(error2);
	}

	[Fact]
	public void Parse_TrailingGarbage_ReturnsError()
	{
		var ok = HostRulesJsonParser.TryParse("{\"AllowLateJoin\": false} x", out _, out var error);

		Assert.False(ok);
		Assert.NotNull(error);
	}
}
