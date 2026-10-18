using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// Where the window may be dragged on its canvas (ticket online-ui-layout-and-input-detail-pass, row 9). The
/// batch `20261018-t` reading is the negative sample: at the up clamp the symmetric rule kept a strip of the
/// FRAME inside the canvas, which at the top edge was the window's BOTTOM — the title bar, the only place a
/// drag can start, sat 411 units above the canvas and the window could not be grabbed again.
/// </summary>
public sealed class OnlineUiWindowDragTests
{
	/// <summary>The canvas the batch read on: the game's own 1920×1080 reference at its screen scale.</summary>
	private const float CanvasWidth = 1920f;

	private const float CanvasHeight = 1080f;

	public static TheoryData<float, float, float, float> CanvasAndWindow => new()
	{
		// The shipped window on the batch's canvas, and the frame a small canvas clamps it to
		// (batch `20261018-t`, row 10: the frame fits 856.89×468 on an 888.89×500 canvas).
		{ CanvasWidth, CanvasHeight, OnlineUiWindowLayout.Width, OnlineUiWindowLayout.Height },
		{ 888.89f, 500f, 856.89f, 468f },
	};

	/// <summary>The title bar stays ON the canvas at the top clamp: it is the one surface a drag starts on, so
	/// a strip of the FRAME is not enough.</summary>
	[Theory]
	[MemberData(nameof(CanvasAndWindow))]
	public void TheTopClampKeepsTheTitleBarOnTheCanvas(float canvasWidth, float canvasHeight, float windowWidth, float windowHeight)
	{
		var (_, y) = OnlineUiWindowDrag.Clamp(canvasWidth, canvasHeight, windowWidth, windowHeight, 0f, 10_000f);
		var top = y + (windowHeight * 0.5f);

		Assert.True(
			top <= canvasHeight * 0.5f,
			$"the window's top edge ({top}) must not rise above the canvas's top edge ({canvasHeight * 0.5f})");
		Assert.True(
			top - OnlineUiWindowLayout.TitleHeight >= -canvasHeight * 0.5f,
			$"the whole title bar ({top - OnlineUiWindowLayout.TitleHeight}..{top}) must stay on the canvas ({-canvasHeight * 0.5f}..{canvasHeight * 0.5f}) so it can be grabbed again");
	}

	/// <summary>The rule this replaces, written out as the negative sample: its up limit lets the title bar
	/// leave the canvas — the batch read 411 units above it — and the rule must pull the same drag back.</summary>
	[Fact]
	public void TheFramesSymmetricTopClampWouldLoseTheTitleBar()
	{
		var firstCut = (CanvasHeight * 0.5f) + (OnlineUiWindowLayout.Height * 0.5f) - OnlineUiWindowDrag.Reach;
		var firstCutTop = firstCut + (OnlineUiWindowLayout.Height * 0.5f);

		Assert.True(
			firstCutTop > CanvasHeight * 0.5f,
			"the negative sample must actually lose the title bar: its top edge should be above the canvas");

		var (_, y) = OnlineUiWindowDrag.Clamp(
			CanvasWidth,
			CanvasHeight,
			OnlineUiWindowLayout.Width,
			OnlineUiWindowLayout.Height,
			0f,
			firstCut);
		Assert.True(
			y + (OnlineUiWindowLayout.Height * 0.5f) <= CanvasHeight * 0.5f,
			$"the same drag must be clamped back to a position whose title bar is on the canvas, not {y}");
	}

	/// <summary>The bottom and both sides keep <see cref="OnlineUiWindowDrag.Reach"/> units of the frame
	/// inside, which is the strip a player can grab — the batch read 1412 / −1412 / 0,842 / 0,−842 on this
	/// canvas.</summary>
	[Fact]
	public void TheBottomAndSideClampsKeepAStripOfTheFrameInside()
	{
		var reach = OnlineUiWindowDrag.Reach;
		var (right, _) = OnlineUiWindowDrag.Clamp(CanvasWidth, CanvasHeight, OnlineUiWindowLayout.Width, OnlineUiWindowLayout.Height, 10_000f, 0f);
		var (left, _) = OnlineUiWindowDrag.Clamp(CanvasWidth, CanvasHeight, OnlineUiWindowLayout.Width, OnlineUiWindowLayout.Height, -10_000f, 0f);
		var (_, down) = OnlineUiWindowDrag.Clamp(CanvasWidth, CanvasHeight, OnlineUiWindowLayout.Width, OnlineUiWindowLayout.Height, 0f, -10_000f);

		Assert.Equal((CanvasWidth * 0.5f) + (OnlineUiWindowLayout.Width * 0.5f) - reach, right);
		Assert.Equal(-right, left);
		Assert.Equal((CanvasHeight * 0.5f) + (OnlineUiWindowLayout.Height * 0.5f) - reach, -down);
		Assert.True(
			down + (OnlineUiWindowLayout.Height * 0.5f) >= (-CanvasHeight * 0.5f) + reach,
			"the window's top edge must stay at least a strip above the canvas's bottom edge");
	}

	/// <summary>The top clamp is the window's own TOP edge, and its value is pinned exactly: with today's
	/// numbers the title bar is shorter than <see cref="OnlineUiWindowDrag.Reach"/>, so the rule's
	/// <c>min(Reach, TitleHeight)</c> keeps the whole bar and the limit is <c>canvas/2 − window/2</c>. A stricter
	/// clamp would still satisfy "the title bar is on the canvas", which is why the exact value — not the
	/// inequality — is what holds the rule.</summary>
	[Theory]
	[MemberData(nameof(CanvasAndWindow))]
	public void TheTopClampIsTheWindowsTopEdge(float canvasWidth, float canvasHeight, float windowWidth, float windowHeight)
	{
		var (_, y) = OnlineUiWindowDrag.Clamp(canvasWidth, canvasHeight, windowWidth, windowHeight, 0f, 10_000f);

		Assert.Equal((canvasHeight * 0.5f) - (windowHeight * 0.5f), y, 3);
	}

	/// <summary>The rule's own arithmetic: the strip it keeps at the top is the title bar's, so the bar must not
	/// be taller than <see cref="OnlineUiWindowDrag.Reach"/> for that strip to be the whole bar. If a later cycle
	/// changes either number, the exact value above must be re-derived rather than silently re-passing.</summary>
	[Fact]
	public void TheTitleBarIsWithinTheReach() =>
		Assert.True(
			OnlineUiWindowLayout.TitleHeight <= OnlineUiWindowDrag.Reach,
			$"the title bar ({OnlineUiWindowLayout.TitleHeight}) must not be taller than the strip the rule keeps ({OnlineUiWindowDrag.Reach})");

	/// <summary>A drag that stays inside the canvas is not moved at all.</summary>
	[Fact]
	public void ADragInsideTheCanvasIsLeftAlone()
	{
		var (x, y) = OnlineUiWindowDrag.Clamp(CanvasWidth, CanvasHeight, OnlineUiWindowLayout.Width, OnlineUiWindowLayout.Height, 120f, -80f);

		Assert.Equal(120f, x);
		Assert.Equal(-80f, y);
	}
}
