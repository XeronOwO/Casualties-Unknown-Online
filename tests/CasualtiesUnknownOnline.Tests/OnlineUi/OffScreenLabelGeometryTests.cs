using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The off-screen marker's label, judged by what a player sees: the label belongs to the marker a member
/// reports as leaving the screen on the right, and its text is a name plus a distance, so its width is not
/// the arrow's. These cases state the property — no part of the label leaves the screen rectangle — and the
/// three answers the ticket asks for when the label cannot fit at all.
/// </summary>
public class OffScreenLabelGeometryTests
{
	private const float ScreenWidth = 1920f;
	private const float ScreenHeight = 1080f;
	private const float Margin = 24f;
	private const float LabelWidth = 260f;
	private const float LabelHeight = 28f;

	[Theory]
	[InlineData(OffScreenArrowDirection.Right)]
	[InlineData(OffScreenArrowDirection.Left)]
	[InlineData(OffScreenArrowDirection.Up)]
	[InlineData(OffScreenArrowDirection.Down)]
	public void AClampedLabel_StaysInsideTheScreen(OffScreenArrowDirection direction)
	{
		// The arrow's own clamp, which is where the label's centre comes from today — the arrow sits ON the
		// guarded rectangle's edge, so a label centred there hangs half its width over the border.
		var (arrowX, arrowY) = direction switch
		{
			OffScreenArrowDirection.Right => (ScreenWidth - Margin, ScreenHeight * 0.5f),
			OffScreenArrowDirection.Left => (Margin, ScreenHeight * 0.5f),
			OffScreenArrowDirection.Up => (ScreenWidth * 0.5f, Margin),
			_ => (ScreenWidth * 0.5f, ScreenHeight - Margin),
		};

		var placement = OffScreenLabelGeometry.Place(
			arrowX,
			arrowY + 40f,
			LabelWidth,
			LabelHeight,
			ScreenWidth,
			ScreenHeight,
			Margin);

		Assert.InRange(placement.X - (LabelWidth * 0.5f), Margin, ScreenWidth - Margin);
		Assert.InRange(placement.Y - (LabelHeight * 0.5f), Margin, ScreenHeight - Margin);
		Assert.InRange(placement.X + (LabelWidth * 0.5f), Margin, ScreenWidth - Margin);
		Assert.InRange(placement.Y + (LabelHeight * 0.5f), Margin, ScreenHeight - Margin);
	}

	[Fact]
	public void ALabelThatCannotFitOnEitherSide_IsPinnedToTheScreenRatherThanPushedOff()
	{
		// Wider than the screen's guarded rectangle: the honest answer is to pin it and let the text run
		// under the border symmetrically, not to move it to a side that has no more room.
		var placement = OffScreenLabelGeometry.Place(
			ScreenWidth - Margin,
			ScreenHeight * 0.5f,
			ScreenWidth,
			LabelHeight,
			ScreenWidth,
			ScreenHeight,
			Margin);

		Assert.Equal(ScreenWidth * 0.5f, placement.X);
		Assert.True(placement.Pinned, "a label with no room on either side reports that it was pinned");
	}

	[Fact]
	public void AHorizontalEdgeLabel_MovesInboardOfTheArrowRatherThanOverTheBorder()
	{
		var placement = OffScreenLabelGeometry.Place(
			ScreenWidth - Margin,
			ScreenHeight * 0.5f + 40f,
			LabelWidth,
			LabelHeight,
			ScreenWidth,
			ScreenHeight,
			Margin);

		Assert.True(placement.X < ScreenWidth - Margin);
		Assert.False(placement.Pinned);
	}

	[Fact]
	public void AVerticalEdgeLabel_KeepsTheArrowsColumn()
	{
		// An up/down arrow's label sits under it and has the whole screen's width: the clamp must leave its
		// column alone rather than shifting it toward the centre.
		var placement = OffScreenLabelGeometry.Place(
			ScreenWidth * 0.5f,
			Margin + 40f,
			LabelWidth,
			LabelHeight,
			ScreenWidth,
			ScreenHeight,
			Margin);

		Assert.Equal(ScreenWidth * 0.5f, placement.X);
		Assert.InRange(placement.Y - (LabelHeight * 0.5f), Margin, ScreenHeight - Margin);
	}

	[Fact]
	public void ADegenerateScreen_LeavesTheLabelWhereItWasAsked()
	{
		var placement = OffScreenLabelGeometry.Place(
			100f,
			50f,
			LabelWidth,
			LabelHeight,
			0f,
			0f,
			Margin);

		Assert.Equal(100f, placement.X);
		Assert.Equal(50f, placement.Y);
		Assert.False(placement.Pinned);
	}
}
