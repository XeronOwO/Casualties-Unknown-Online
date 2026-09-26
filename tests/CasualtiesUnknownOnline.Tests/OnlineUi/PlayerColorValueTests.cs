using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The free player colour's codec (ticket online-ui-art-and-controls-overhaul, S3): the text the hex
/// field shows, the configuration stores and the picker compares by. Three facts carry the design — the
/// two accepted forms round-trip through their own text, a value that is still being typed never parses
/// (which is why the three-digit shorthand the game's console accepts is refused here), and nothing else
/// is guessed at — so a colour the picker stores is always a colour the picker reads back.
/// </summary>
public sealed class PlayerColorValueTests
{
	[Fact]
	public void AnOpaqueColorIsWrittenAsSixDigits() =>
		Assert.Equal("#FF8000", new PlayerColorValue(1f, 0.5f, 0f, 1f).ToHexString());

	[Fact]
	public void ATranslucentColorKeepsItsAlpha() =>
		Assert.Equal("#FF800080", new PlayerColorValue(1f, 0.5f, 0f, 0.5f).ToHexString());

	[Fact]
	public void BothFormsRoundTripThroughTheirOwnText()
	{
		string[] forms = ["#000000", "#FFFFFF", "#3C8CD8", "#00000000", "#3C8CD880", "#80000080"];
		foreach (var text in forms)
		{
			Assert.True(PlayerColorValue.TryParseHex(text, out var color), $"{text} is one of the two accepted forms and must parse");
			Assert.Equal(text, color.ToHexString());
		}
	}

	/// <summary>Eight digits carrying a fully opaque alpha are the same colour as six, and the shorter form
	/// is what gets stored — a colour has one canonical text, which is what the picker compares by.</summary>
	[Fact]
	public void AnOpaqueValueWrittenWithItsAlphaComesBackAsSixDigits()
	{
		Assert.True(PlayerColorValue.TryParseHex("#FF0000FF", out var color));
		Assert.Equal("#FF0000", color.ToHexString());
	}

	[Fact]
	public void ParsingIsCaseInsensitiveAndIgnoresSurroundingSpace()
	{
		Assert.True(PlayerColorValue.TryParseHex("  #3c8cd8  ", out var color));
		Assert.Equal("#3C8CD8", color.ToHexString());
	}

	[Fact]
	public void AColorStillBeingTypedIsNotAColorYet() =>
		Assert.False(
			PlayerColorValue.TryParseHex("#FF0", out _),
			"the three-digit shorthand would commit a colour on the way to #FF0000, so it is refused");

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("#")]
	[InlineData("#FFF")]
	[InlineData("#FFFF")]
	[InlineData("#FFFFF")]
	[InlineData("#3C8CD8F")]
	[InlineData("#3C8CD8FFF")]
	[InlineData("#GGGGGG")]
	[InlineData("#3C8CDZ")]
	[InlineData("#3C8CD 8")]
	[InlineData("3C8CD8")]
	[InlineData("red")]
	[InlineData("0x3C8CD8")]
	public void AnythingElseIsRefused(string? text) =>
		Assert.False(PlayerColorValue.TryParseHex(text, out _), $"`{text}` is not one of the two accepted forms");
}
