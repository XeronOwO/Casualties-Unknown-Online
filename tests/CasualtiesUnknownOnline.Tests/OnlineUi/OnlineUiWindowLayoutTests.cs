using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The Online UI window's shell arithmetic (ticket online-ui-layout-and-input-detail-pass, S1; the user's
/// acceptance pass of 2026-09-27 asked for room between the tabs and the body of a page). The three bands of
/// the shell — the title bar, the tab strip and the page — are stacked from the numbers here, and the
/// negative sample is the arithmetic the acceptance pass actually ran against: the first cut placed the
/// page's top edge at <c>TitleHeight + TabHeight + 2 × the row gap</c>, which is the frame's own padding
/// short of where the tab strip ends.
/// </summary>
public sealed class OnlineUiWindowLayoutTests
{
	/// <summary>The row gap the first cut used, written out because the layout no longer carries one: it is
	/// part of the negative sample, not part of the rule.</summary>
	private const float FirstCutRowGap = 4f;

	[Fact]
	public void ThePageStartsBelowTheTabStripWithAGapOfItsOwn() =>
		Assert.True(
			OnlineUiWindowLayout.PageTop >= OnlineUiWindowLayout.TabBottom + OnlineUiWindowLayout.TabGap,
			$"the page's top edge ({OnlineUiWindowLayout.PageTop}) must clear the tab strip's bottom edge ({OnlineUiWindowLayout.TabBottom}) by the page's gap ({OnlineUiWindowLayout.TabGap})");

	[Fact]
	public void TheFirstCutsArithmeticWouldNotClearTheTabStrip()
	{
		var firstCut = OnlineUiWindowLayout.TitleHeight + OnlineUiWindowLayout.TabHeight + (2f * FirstCutRowGap);

		Assert.True(
			firstCut < OnlineUiWindowLayout.TabBottom,
			$"the first cut's inset ({firstCut}) must be less than the tab strip's bottom edge ({OnlineUiWindowLayout.TabBottom}) — that difference is the defect this rule exists to reject, and if it ever stops holding the rule has stopped proving anything");
	}

	[Fact]
	public void TheBandsAreStackedFromTheTopEdge()
	{
		Assert.Equal(OnlineUiWindowLayout.Padding, OnlineUiWindowLayout.TitleTop);
		Assert.Equal(
			OnlineUiWindowLayout.TitleTop + OnlineUiWindowLayout.TitleHeight + OnlineUiWindowLayout.TabGap,
			OnlineUiWindowLayout.TabTop);
		Assert.Equal(OnlineUiWindowLayout.TabTop + OnlineUiWindowLayout.TabHeight, OnlineUiWindowLayout.TabBottom);
		Assert.Equal(OnlineUiWindowLayout.TabBottom + OnlineUiWindowLayout.TabGap, OnlineUiWindowLayout.PageTop);
	}

	[Fact]
	public void TheFrameHoldsThePageItFrames()
	{
		Assert.Equal(OnlineUiWindowLayout.Width - (2f * OnlineUiWindowLayout.Padding), OnlineUiWindowLayout.ContentWidth);
		Assert.Equal(
			OnlineUiWindowLayout.Height - OnlineUiWindowLayout.PageTop - OnlineUiWindowLayout.Padding,
			OnlineUiWindowLayout.PageHeight);
		Assert.True(OnlineUiWindowLayout.ContentWidth > 0f, "the page must have a width to lay its rows out in");
		Assert.True(OnlineUiWindowLayout.PageHeight > 0f, "the page must have a height to lay its rows out in");
	}

	/// <summary>The frame the user asked to be larger: at least 200 units wider and 140 taller than the rect
	/// the first cut used (780 × 540), with the text and the controls inside it unchanged.</summary>
	[Fact]
	public void TheFrameIsLargerThanTheOneItReplaces()
	{
		Assert.True(OnlineUiWindowLayout.Width >= 780f + 200f, $"the frame must be wider than the first cut's 780, not {OnlineUiWindowLayout.Width}");
		Assert.True(OnlineUiWindowLayout.Height >= 540f + 140f, $"the frame must be taller than the first cut's 540, not {OnlineUiWindowLayout.Height}");
	}

	/// <summary>One compact height for every control of a page, at the tab strip's own height: the user's
	/// answer to the acceptance pass' "the buttons are the game's own, much taller rows".</summary>
	[Fact]
	public void EveryControlSharesTheTabStripsHeight() =>
		Assert.Equal(OnlineUiWindowLayout.TabHeight, OnlineUiWindowLayout.ControlHeight);

	/// <summary>
	/// The page's vertical rhythm: a heading is separated from whatever came before it by clearly more than two
	/// ordinary rows, and bound to its own block by less than that. The acceptance pass found section headings
	/// with no room above them — the gaps were in each drawer's memory rather than in one scale.
	/// </summary>
	[Fact]
	public void ThePageRhythmSeparatesAHeadingFromWhatItOpens()
	{
		Assert.True(
			OnlineUiWindowLayout.SectionGap > 2f * OnlineUiWindowLayout.RowGap,
			$"a heading's own room ({OnlineUiWindowLayout.SectionGap}) must be more than two rows ({OnlineUiWindowLayout.RowGap} each)");
		Assert.True(
			OnlineUiWindowLayout.SectionBodyGap > 0f && OnlineUiWindowLayout.SectionBodyGap < OnlineUiWindowLayout.SectionGap,
			$"the room below a heading ({OnlineUiWindowLayout.SectionBodyGap}) must be positive and smaller than the room above it ({OnlineUiWindowLayout.SectionGap}), so the heading reads as its block's own");
		Assert.True(
			OnlineUiWindowLayout.BlockGap >= OnlineUiWindowLayout.RowGap,
			$"two blocks with no heading between them ({OnlineUiWindowLayout.BlockGap}) must be at least one row apart ({OnlineUiWindowLayout.RowGap})");
	}

	/// <summary>A space row is room and nothing else, and it carries the room the page asked for.</summary>
	[Fact]
	public void ASpaceRowCarriesOnlyRoom()
	{
		var space = OnlineUiRowModel.Space(OnlineUiWindowLayout.SectionGap);

		Assert.True(space.IsSpace, "a space row holds no elements");
		Assert.True(
			space.Gap.Equals(OnlineUiWindowLayout.SectionGap),
			$"a space row carries its own room, not {space.Gap}");
		Assert.False(
			OnlineUiRowModel.Of(OnlineUiElementModel.Label("a row")).IsSpace,
			"a row with an element is not a space row");
	}
}
