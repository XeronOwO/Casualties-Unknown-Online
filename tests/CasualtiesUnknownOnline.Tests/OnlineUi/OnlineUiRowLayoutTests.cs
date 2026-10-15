using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The row-wrapping rule the Online UI window lays its pages out with (ticket
/// online-ui-art-and-controls-overhaul, S2b). The rule is pure and the adapter consumes its answer, so
/// whether a member's twelve action buttons form two lines or three is decided — and tested — without a
/// Unity runtime.
/// </summary>
public sealed class OnlineUiRowLayoutTests
{
	[Fact]
	public void AnEmptyRowHasNoLines() => Assert.Empty(OnlineUiRowLayout.LineOf([], 400f));

	[Fact]
	public void ElementsThatFitStayOnOneLine() => Assert.Equal([0, 0, 0, 0, 0], OnlineUiRowLayout.LineOf([70f, 90f, 110f, 130f, 150f], 600f));

	/// <summary>Five 180-wide buttons against a 400-wide content: two, two, one.</summary>
	[Fact]
	public void ElementsThatDoNotFitWrapInOrder() =>
		Assert.Equal([0, 0, 1, 1, 2], OnlineUiRowLayout.LineOf([180f, 180f, 180f, 180f, 180f], 400f));

	/// <summary>A width hint of 0 is a natural-width control (a label): it counts as nothing towards the
	/// line, which is what keeps a <c>[label, button]</c> row on one line.</summary>
	[Fact]
	public void ANaturalWidthElementNeverForcesABreak()
	{
		Assert.Equal([0, 0, 0], OnlineUiRowLayout.LineOf([0f, 0f, 0f], 100f));
		Assert.Equal([0, 0, 1], OnlineUiRowLayout.LineOf([180f, 0f, 180f], 200f));
	}

	/// <summary>An element wider than the whole content still gets a line of its own rather than pushing
	/// every later element to a new line for ever.</summary>
	[Fact]
	public void AnElementWiderThanTheContentGetsItsOwnLine() => Assert.Equal([0, 1], OnlineUiRowLayout.LineOf([900f, 10f], 400f));

	/// <summary>The gap the consuming layout puts between two elements of one line is part of the fit: two
	/// 200-wide buttons and a 4-unit gap do not fit a 400-wide content, so the second starts a line — which
	/// is exactly what the uGUI layout group will do with them.</summary>
	[Fact]
	public void TheLineSpacingCountsTowardsTheFit() => Assert.Equal([0, 1], OnlineUiRowLayout.LineOf([200f, 200f], 400f, spacing: 4f));

	/// <summary>Without a spacing the same two elements fit exactly, which is the arithmetic the cases
	/// above exercise.</summary>
	[Fact]
	public void NoSpacingKeepsTheSameTwoElementsOnOneLine() => Assert.Equal([0, 0], OnlineUiRowLayout.LineOf([200f, 200f], 400f));

	/// <summary>A content width of 0 or less cannot break anything — the guard keeps a degenerate layout
	/// call from turning one element into one line each.</summary>
	[Fact]
	public void AContentWidthOfZeroKeepsEverythingOnOneLine()
	{
		Assert.Equal([0, 0, 0], OnlineUiRowLayout.LineOf([100f, 100f, 100f], 0f));
		Assert.Equal([0, 0], OnlineUiRowLayout.LineOf([100f, 100f], -50f));
	}

	[Fact]
	public void TheLineNumbersNeverSkip()
	{
		var lines = OnlineUiRowLayout.LineOf([120f, 120f, 120f, 120f, 120f, 120f, 120f], 360f);

		Assert.Equal(0, lines[0]);
		for (var index = 1; index < lines.Length; index++)
		{
			Assert.True(
				lines[index] == lines[index - 1] || lines[index] == lines[index - 1] + 1,
				$"line {index} jumped from {lines[index - 1]} to {lines[index]}");
		}
	}

	/// <summary>A control the model puts on its own line starts the line BELOW its name, whether or not it
	/// would have fitted: that is the paired control of a page — the name above, the slider and its value
	/// below (ticket online-ui-paired-controls-and-page-scroll, S3).</summary>
	[Fact]
	public void AMarkedElementStartsTheLineBelow()
	{
		// The label takes no width, so the fit alone would keep all three on one line.
		Assert.Equal([0, 1, 1], OnlineUiRowLayout.LineOf([0f, 200f, 40f], [false, true, false], 600f));
		Assert.Equal([0, 1, 1], OnlineUiRowLayout.LineOf([0f, 200f, 40f], [false, true, false], 600f, spacing: 4f));
	}

	/// <summary>The mark is a FLOOR on the wrap, not a replacement for it: an element sent to its own line
	/// that is still too wide for it wraps again, exactly as an unmarked over-wide element does.</summary>
	[Fact]
	public void AMarkedElementThatDoesNotFitWrapsAgain() =>
		// 0, then a 700-wide control against a 400-wide content: its own line, and the 300 beside it wraps.
		Assert.Equal([0, 1, 2], OnlineUiRowLayout.LineOf([0f, 700f, 300f], [false, true, false], 400f, spacing: 4f));

	/// <summary>A mark on the FIRST element of a row changes nothing: there is no line above it to leave, and
	/// the answers have to stay a run of line indices that never skips a number (line 1 would be a line no
	/// element is on).</summary>
	[Fact]
	public void AMarkOnTheFirstElementChangesNothing() =>
		Assert.Equal([0, 0], OnlineUiRowLayout.LineOf([0f, 40f], [true, false], 600f));

	/// <summary>An element the fit already sent to a line of its own keeps that line: the mark never pulls an
	/// element back up onto the line above.</summary>
	[Fact]
	public void AMarkedElementKeepsTheLineTheFitGaveIt() =>
		// The second element cannot fit beside the first, so it is already on line 1; its mark asks for line
		// 1 as well and must not push it to 2.
		Assert.Equal([0, 1], OnlineUiRowLayout.LineOf([300f, 300f], [false, true], 400f));

	/// <summary>Without marks the two answers are the same rule: the overload that takes them is the plain
	/// one plus the model's own breaks.</summary>
	[Fact]
	public void NoMarksGiveTheSameAnswerAsThePlainRule() =>
		Assert.Equal(
			OnlineUiRowLayout.LineOf([180f, 180f, 180f, 180f, 180f], 400f),
			OnlineUiRowLayout.LineOf([180f, 180f, 180f, 180f, 180f], [false, false, false, false, false], 400f));

	/// <summary>
	/// The line a paired control was sent to is ITS line: the name above owns nothing, the control owns the
	/// line it opens and everything the fit puts beside it on that line belongs to it — that is the row the
	/// adapter lays those elements out inside (ticket online-ui-paired-controls-and-page-scroll, S3).
	/// </summary>
	[Fact]
	public void APairedControlOwnsTheLineItOpens()
	{
		var lines = OnlineUiRowLayout.LineOf([0f, 200f, 40f], [false, true, false], 600f);

		Assert.Equal([-1, 1, 1], OnlineUiRowLayout.LineOwners([false, true, false], lines));
	}

	/// <summary>A line the FIT produced owns nothing: only a control the model sent to a line of its own holds
	/// one, so a wrapped line's elements are laid out on the line itself.</summary>
	[Fact]
	public void ALineTheFitProducedOwnsNothing()
	{
		// 600 wide of content: the third element wraps, so line 1 is the fit's own.
		var lines = OnlineUiRowLayout.LineOf([300f, 300f, 300f], 600f);

		Assert.Equal([0, 1], [lines[0], lines[2]]);
		Assert.Equal([-1, -1, -1], OnlineUiRowLayout.LineOwners([false, false, false], lines));
	}

	/// <summary>A mark on the element that already opens a line owns it, and an element the fit pushed to the
	/// NEXT line after a paired row starts a line of its own that nothing holds: the ownership never reaches
	/// past the line it was given.</summary>
	[Fact]
	public void TheOwnershipStopsAtTheLineItWasGiven()
	{
		// 0, then a 700-wide control on its own line, then a 300-wide one that wraps again.
		var lines = OnlineUiRowLayout.LineOf([0f, 700f, 300f], [false, true, false], 400f, spacing: 4f);

		Assert.Equal([0, 1, 2], lines);
		Assert.Equal([-1, 1, -1], OnlineUiRowLayout.LineOwners([false, true, false], lines));
	}
}
