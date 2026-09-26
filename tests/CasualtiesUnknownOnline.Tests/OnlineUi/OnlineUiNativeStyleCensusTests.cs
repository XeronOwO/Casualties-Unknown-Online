using System.Linq;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The census fold: the adapter reports one row per object it found, and the log needs a bounded,
/// deterministic summary of that. The cases here are the contract the probe leans on — duplicates
/// collapse with a count, frequency orders the rows, a tie is broken by name so two runs of the same
/// screen produce the same lines, the cap trims the tail rather than the head, and a style that
/// differs only in its border is a different style (that difference is the 9-slice fact).
/// </summary>
public sealed class OnlineUiNativeStyleCensusTests
{
	[Fact]
	public void AnEmptyCandidateListCollapsesToNothing()
	{
		Assert.Empty(OnlineUiNativeStyleCensus.CollapseImageStyles([]));
		Assert.Empty(OnlineUiNativeStyleCensus.CollapseTextStyles([]));
	}

	[Fact]
	public void EqualStylesCollapseIntoOneRowCarryingTheirCount()
	{
		var census = OnlineUiNativeStyleCensus.CollapseImageStyles(
			[Image("UIPanel"), Image("UIPanel"), Image("UIPanel")]);

		var row = Assert.Single(census);
		Assert.True(row.Occurrences == 3, $"three equal styles must report x3, reported x{row.Occurrences}");
	}

	[Fact]
	public void TheMostCommonStyleComesFirst()
	{
		var census = OnlineUiNativeStyleCensus.CollapseImageStyles(
			[Image("Rare"), Image("Common"), Image("Common"), Image("Common")]);

		Assert.True(census.Count == 2, $"two distinct styles expected, got {census.Count}");
		Assert.True(census[0].SpriteName == "Common", $"the common style must lead, got {census[0].SpriteName}");
	}

	[Fact]
	public void ATieIsBrokenByNameSoTheCensusIsDeterministic()
	{
		var census = OnlineUiNativeStyleCensus.CollapseImageStyles([Image("Beta"), Image("Alpha")]);

		Assert.True(census[0].SpriteName == "Alpha", $"a tie must order by name, got {census[0].SpriteName} first");
	}

	[Fact]
	public void TheCapKeepsTheMostCommonRowsAndTrimsTheTail()
	{
		var census = OnlineUiNativeStyleCensus.CollapseImageStyles(
			[Image("Once"), Image("Twice"), Image("Twice"), Image("Thrice", border: 0f), Image("Thrice", border: 0f), Image("Thrice", border: 0f)],
			cap: 2);

		Assert.True(census.Count == 2, $"the cap must bound the census, got {census.Count}");
		Assert.True(census[0].SpriteName == "Thrice", $"the cap must trim the tail, not the head: {census[0].SpriteName}");
	}

	[Fact]
	public void ANonPositiveCapProducesNothing()
	{
		Assert.Empty(OnlineUiNativeStyleCensus.CollapseImageStyles([Image("UIPanel")], cap: 0));
		Assert.Empty(OnlineUiNativeStyleCensus.CollapseTextStyles([Text("SDF", 14f)], cap: -1));
	}

	[Fact]
	public void AStyleThatDiffersOnlyInItsBorderIsNotTheSameStyle()
	{
		var census = OnlineUiNativeStyleCensus.CollapseImageStyles(
			[Image("UIPanel", border: 0f), Image("UIPanel", border: 12f)]);

		Assert.True(census.Count == 2, $"9-sliced and plain are two styles, got {census.Count}");
		Assert.True(census.Count(style => style.IsNineSliced) == 1, "exactly one row must carry the 9-slice fact");
	}

	[Fact]
	public void TextStylesCollapseByFontSizeAndColour()
	{
		var census = OnlineUiNativeStyleCensus.CollapseTextStyles(
			[Text("SDF", 14f), Text("SDF", 18f), Text("SDF", 14f)]);

		Assert.True(census.Count == 2, $"the size is part of the style, got {census.Count}");
		Assert.True(census[0].FontSize == 14f, $"the common size must lead, got {census[0].FontSize}");
		Assert.True(census[0].Occurrences == 2, $"the common size must carry its count, got {census[0].Occurrences}");
	}

	private static OnlineUiNativeImageStyle Image(string sprite, float border = 0f) => new(
		"Special/GameSettingDropdown",
		$"CUO Online UI Native Host/{sprite}",
		sprite,
		"Sliced",
		1f,
		border,
		border,
		border,
		border,
		new OnlineUiNativeRgba(0.1f, 0.2f, 0.3f, 1f),
		1);

	private static OnlineUiNativeTextStyle Text(string fontAsset, float size) => new(
		"Special/GameSettingDropdown",
		"CUO Online UI Native Host/Label",
		fontAsset,
		size,
		new OnlineUiNativeRgba(1f, 1f, 1f, 1f),
		1);
}
