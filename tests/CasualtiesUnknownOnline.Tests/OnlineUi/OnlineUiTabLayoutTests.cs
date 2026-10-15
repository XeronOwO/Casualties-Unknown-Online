using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The rule that shares the window's tab strip among its tabs (ticket online-ui-layout-and-input-detail-pass:
/// the English strip the user found on 2026-10-14). A tab is as wide as its own caption and the strip is a band
/// of a fixed width, so the tabs together have to fit it — before this rule nothing made them, and the six
/// English tabs measured 1038.7 units inside a 984-unit page with the last one past the window's edge.
///
/// <para>
/// The rule is pure and the adapter only reads its answer, so what a strip gives up, to whom, and what it
/// refuses to give up are decided — and tested — without a Unity runtime. The widths in
/// <see cref="TheEnglishCaptionsTheUserFoundFitTheStrip"/> are the LIVE reading of that run, caption by caption
/// (batch `20261015-a`), which is what makes the last case the user's own report rather than a made-up strip.
/// </para>
/// </summary>
public sealed class OnlineUiTabLayoutTests
{
	/// <summary>The floor a tab's model declares, in the window's own units: <c>OnlineUiPageBuilder.TabWidth</c>,
	/// which is a floor and nothing more. The value is repeated here because the plugin is not referenced by this
	/// suite (its source is), and <see cref="OnlineUiControlWidthPinTests.ATabStripFitsItsOwnBand"/> is what holds
	/// the two together: a floor that moves fails there.</summary>
	private const float TabFloor = 112f;

	/// <summary>The room a caption keeps on each side of its text at the game's own size: one font size, which
	/// is 20 units on the live reading this class is calibrated with.</summary>
	private const float CaptionRoom = 2f * 20f;

	/// <summary>The room a caption keeps at its narrowest — the room the control's own box keeps, which is what
	/// a tab in a strip that cannot fit every caption gives up first.</summary>
	private const float MinimumRoom = 2f * OnlineUiWindowLayout.ControlInnerPadding;

	/// <summary>The page's own width: the window minus its padding on both sides, which is the band the strip
	/// has (the live reading says 984).</summary>
	private const float StripWidth = OnlineUiWindowLayout.ContentWidth;

	[Fact]
	public void AnEmptyStripHasNoWidths() =>
		Assert.Empty(OnlineUiTabLayout.Widths([], [], StripWidth, spacing: 4f));

	[Fact]
	public void ATabKeepsWhatItAskedForWhileTheStripFits()
	{
		Assert.Equal([200f], OnlineUiTabLayout.Widths([200f], [100f], StripWidth, spacing: 4f));
		Assert.Equal([200f, 300f], OnlineUiTabLayout.Widths([200f, 300f], [150f, 200f], StripWidth, spacing: 4f));
	}

	/// <summary>A strip that is filled exactly gives nothing up — the boundary an off-by-one would turn into a
	/// strip that shrinks every tab by a fraction of a unit.</summary>
	[Fact]
	public void AStripThatFitsExactlyGivesNothingUp() =>
		Assert.Equal([200f, 200f], OnlineUiTabLayout.Widths([200f, 200f], [100f, 100f], 404f, spacing: 4f));

	/// <summary>The gap between two tabs is the strip's to pay for: two tabs that fit 400 units on their own do
	/// not fit it once the layout's own gap sits between them, and the layout group would push the second tab
	/// past the band while this rule still called the pair one line of tabs.</summary>
	[Fact]
	public void TheGapsBetweenTheTabsCountTowardsTheStrip() =>
		Assert.Equal([198f, 198f], OnlineUiTabLayout.Widths([200f, 200f], [100f, 100f], 400f, spacing: 4f));

	/// <summary>The excess comes off the tabs that have room to spare, in proportion to that room: the tab with
	/// 150 units of room gives up half again as much as the tab with 100, and neither loses more than it has.</summary>
	[Fact]
	public void TheExcessComesOffTheTabsThatHaveRoomToSpare()
	{
		// 500 asked against a 460 strip: 40 to give up, over 250 units of room (100 + 150).
		var widths = OnlineUiTabLayout.Widths([200f, 300f], [100f, 150f], 460f);

		Assert.Equal(200f - (40f * 100f / 250f), widths[0], 3);
		Assert.Equal(300f - (40f * 150f / 250f), widths[1], 3);
		Assert.Equal(460f, widths[0] + widths[1], 3);
	}

	[Fact]
	public void NoTabGoesUnderItsOwnMinimum()
	{
		// 600 asked against 500, with 220 units of room between the two tabs: the tab that can barely shrink
		// stops at its floor instead of giving up a caption.
		var widths = OnlineUiTabLayout.Widths([300f, 300f], [280f, 100f], 500f);

		Assert.True(widths[0] >= 280f, $"a tab went under its own floor: {widths[0]} < 280");
		Assert.True(widths[1] >= 100f, $"a tab went under its own floor: {widths[1]} < 100");
		Assert.Equal(500f, widths[0] + widths[1], 3);
	}

	/// <summary>A tab whose minimum is not declared — or is not positive — cannot shrink at all: its own width
	/// stands and the excess comes off whatever else has room. A control with no caption of its own answers this
	/// way, and a rule that quietly shrank it to nothing would be a control the player cannot see.</summary>
	[Fact]
	public void ATabWithNoMinimumCannotShrink()
	{
		Assert.Equal([300f, 160f], OnlineUiTabLayout.Widths([300f, 200f], [0f, 160f], 460f));
		Assert.Equal([300f, 300f], OnlineUiTabLayout.Widths([300f, 300f], [0f], 500f));
	}

	/// <summary>A strip too narrow for even the captions keeps them: cutting a caption is the worse of the two
	/// answers, and the caller can still see that the strip is over its band, because what it gets back is what
	/// it asked for.</summary>
	[Fact]
	public void AStripTooNarrowForEveryCaptionKeepsTheCaptions() =>
		Assert.Equal([280f, 280f], OnlineUiTabLayout.Widths([300f, 300f], [280f, 280f], 500f));

	/// <summary>A tab cannot be asked for a negative width; the rule reads one as nothing rather than handing
	/// the layout a rect the player cannot see.</summary>
	[Fact]
	public void AWidthThatIsNotThereIsZeroAndNotNegative() =>
		Assert.Equal([0f, 200f], OnlineUiTabLayout.Widths([-40f, 200f], [0f, 100f], StripWidth));

	/// <summary>
	/// The user's own report, with the six English captions the run measured (batch `20261015-a`: `Home` 58.6,
	/// `Players` 92.1, `Network` 93.8, `Admin` 63.6, `Worlds` 79.5, `Preferences` 151.2, at the game's size 20).
	/// The two rooms are the ones the rule is given: the room a row keeps around a caption for the width a tab
	/// ASKS for, and the room a control's own box keeps for the least it can be drawn at.
	///
	/// <para>
	/// The case carries both halves of the fix. Widths asked with the room counted TWICE are the 1038.7 units
	/// the user saw, and the strip shares its band instead of overflowing it; widths asked with the room counted
	/// once need no shrinking at all, which is what a tab is supposed to be — a caption with room around it, not
	/// a caption with the room added on top of it twice.
	/// </para>
	/// </summary>
	[Fact]
	public void TheEnglishCaptionsTheUserFoundFitTheStrip()
	{
		float[] captions = [58.6f, 92.1f, 93.8f, 63.6f, 79.5f, 151.2f];
		var spacing = 4f;
		var minimum = new List<float>();
		var askedTwice = new List<float>();
		var askedOnce = new List<float>();
		foreach (var caption in captions)
		{
			minimum.Add(OnlineUiControlBox.CaptionedWidth(TabFloor, caption, MinimumRoom));
			askedTwice.Add(OnlineUiControlBox.CaptionedWidth(TabFloor, caption, 2f * CaptionRoom));
			askedOnce.Add(OnlineUiControlBox.CaptionedWidth(TabFloor, caption, CaptionRoom));
		}

		Assert.True(
			Total(askedTwice, spacing) > StripWidth,
			$"these are the widths the user's run measured: they must be over the band, or this case is not the report it claims to be ({Total(askedTwice, spacing)} <= {StripWidth})");

		var shared = OnlineUiTabLayout.Widths(askedTwice, minimum, StripWidth, spacing);
		Assert.True(Total(shared, spacing) <= StripWidth, $"the English strip still runs past its band: {Total(shared, spacing)}");

		var untouched = OnlineUiTabLayout.Widths(askedOnce, minimum, StripWidth, spacing);
		Assert.Equal(askedOnce, untouched);
		Assert.True(Total(untouched, spacing) <= StripWidth, $"the English strip does not fit its band once the room is counted once: {Total(untouched, spacing)}");
	}

	private static float Total(IReadOnlyList<float> widths, float spacing)
	{
		var total = 0f;
		foreach (var width in widths)
		{
			total += width;
		}

		return total + (spacing * Math.Max(0, widths.Count - 1));
	}
}
