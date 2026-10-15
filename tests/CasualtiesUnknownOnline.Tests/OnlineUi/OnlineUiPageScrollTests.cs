using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The arithmetic behind a page's scroll affordance (ticket online-ui-paired-controls-and-page-scroll, S4),
/// settled without a Unity runtime for the reason the wrap and the width rules are: the user's report is a
/// page whose content runs past what is shown with nothing on the right to say so, and the whole answer is
/// "how big is the handle, where does it stand, and what does a click on the track mean".
///
/// <para>
/// The negatives are the shapes that make a scrollbar useless: a bar over a page that fits (a control that
/// does nothing), a handle of zero height on a very long page (something the player cannot grab), and a
/// position that runs past either end of the page.
/// </para>
/// </summary>
public sealed class OnlineUiPageScrollTests
{
	/// <summary>A page that fits shows nothing: the caller draws no bar at all, and this is how it knows.</summary>
	[Fact]
	public void APageThatFitsHasTheWholeTrack()
	{
		Assert.Equal(OnlineUiPageScroll.Single, OnlineUiPageScroll.For(viewportHeight: 600f, contentHeight: 600f, position: 1f));
		Assert.Equal(OnlineUiPageScroll.Single, OnlineUiPageScroll.For(viewportHeight: 600f, contentHeight: 400f, position: 0.5f));
		Assert.Equal(1f, OnlineUiPageScroll.Single.Fraction);
	}

	/// <summary>A page a fraction of a unit taller than its viewport still fits: a handle that moves by less
	/// than a pixel is a bar that does not work.</summary>
	[Fact]
	public void APageTallerByNothingMeasurableStillFits() =>
		Assert.Equal(
			OnlineUiPageScroll.Single,
			OnlineUiPageScroll.For(viewportHeight: 600f, contentHeight: 600.2f, position: 1f));

	/// <summary>A viewport the engine has not laid out yet (the first frame of the surface) answers the same
	/// as a page that fits, so the bar cannot flash at full size before anything is measured.</summary>
	[Fact]
	public void AnUnmeasuredViewportHasNoHandle()
	{
		Assert.Equal(OnlineUiPageScroll.Single, OnlineUiPageScroll.For(viewportHeight: 0f, contentHeight: 1200f, position: 1f));
		Assert.Equal(OnlineUiPageScroll.Single, OnlineUiPageScroll.For(viewportHeight: -5f, contentHeight: 1200f, position: 1f));
	}

	/// <summary>The handle fills the share of the track the player can see: half the content, half the
	/// track.</summary>
	[Fact]
	public void TheHandleFillsTheShareTheViewportShows() =>
		Assert.Equal(0.5f, OnlineUiPageScroll.For(viewportHeight: 600f, contentHeight: 1200f, position: 1f).Fraction);

	/// <summary>At the top of the page the handle stands at the top of the track, and at its end at the
	/// bottom: the page's own position is 1 at the top, which is the engine's convention.</summary>
	[Fact]
	public void TheHandleStandsWhereThePageIsScrolledTo()
	{
		Assert.Equal(0f, OnlineUiPageScroll.For(600f, 1200f, position: 1f).Offset);
		Assert.Equal(1f, OnlineUiPageScroll.For(600f, 1200f, position: 0f).Offset);
		Assert.Equal(0.5f, OnlineUiPageScroll.For(600f, 1200f, position: 0.5f).Offset);
	}

	/// <summary>A very long page keeps a handle the player can still see and grab, even though its exact share
	/// of the track would be a hairline.</summary>
	[Fact]
	public void AVeryLongPageKeepsAGrabbableHandle()
	{
		var handle = OnlineUiPageScroll.For(viewportHeight: 600f, contentHeight: 60_000f, position: 1f);

		Assert.Equal(0.05f, handle.Fraction);
		Assert.True(handle.Fraction > 0f, "a handle of zero height is a bar nobody can drag");
	}

	/// <summary>A position outside the page is pulled back to its end: the same rule the adapter writes
	/// through, so a click past the bottom of the track lands at the bottom of the page rather than past
	/// it.</summary>
	[Fact]
	public void APositionOutsideThePageIsClamped()
	{
		Assert.Equal(0f, OnlineUiPageScroll.PositionFor(2f));
		Assert.Equal(1f, OnlineUiPageScroll.PositionFor(-1f));
		Assert.Equal(1f, OnlineUiPageScroll.PositionFor(0f));
		Assert.Equal(0f, OnlineUiPageScroll.PositionFor(1f));
		Assert.Equal(0.5f, OnlineUiPageScroll.PositionFor(0.5f));
	}

	/// <summary>
	/// The room the bar takes on the page's band is the room the page's CONTENT keeps clear of, and the two are
	/// one number: the bar is drawn inside the outermost units of the band, so the content stops where the track
	/// begins and no row is drawn under it (the user's first point of 2026-10-15: the value box ended at 486.00
	/// while the bar's track spanned 482.00..490.00, i.e. four units of the box sat under the bar).
	/// </summary>
	[Fact]
	public void TheBarTakesTheRoomTheContentKeepsClearOf()
	{
		Assert.Equal(OnlineUiPageScroll.TrackInset + OnlineUiPageScroll.TrackWidth, OnlineUiPageScroll.GutterWidth);
		Assert.True(OnlineUiPageScroll.TrackWidth > 0f, "a bar of no width is a bar nobody can see or grab");
		Assert.True(OnlineUiPageScroll.TrackInset >= 0f, "the bar must stay inside the band it rides");
		Assert.True(
			OnlineUiPageScroll.GutterWidth >= OnlineUiPageScroll.TrackWidth,
			"the content must clear the whole track, so the room the bar keeps from the band's edge is room the content does not get either");
	}

	/// <summary>The track's offset and the page's position are one rule read both ways: a click where the
	/// handle stands asks for the position the handle was drawn for.</summary>
	[Fact]
	public void TheHandleAndThePageAgreeBothWays()
	{
		foreach (var position in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
		{
			var handle = OnlineUiPageScroll.For(600f, 2400f, position);
			Assert.Equal(position, OnlineUiPageScroll.PositionFor(handle.Offset), precision: 4);
		}
	}
}
