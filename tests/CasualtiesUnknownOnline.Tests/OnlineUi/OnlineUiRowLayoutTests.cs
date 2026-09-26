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
}
