using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// Where a point-anchored panel's corner goes (ticket online-ui-art-and-controls-overhaul, S5): the rule the
/// IMGUI context menu applied when it clamped its own rect into the screen, moved into the Runtime where it
/// can be tested — the plugin asks it, the adapter converts the answer to its canvas.
///
/// <para>
/// The cases are the shape's own edges: the pointer offset, both screen edges, and a panel larger than the
/// screen (which hangs from the top, so its FIRST rows stay visible instead of being pushed off the top).
/// </para>
/// </summary>
public sealed class OnlineUiPanelPlacementTests
{
	private const int ScreenWidth = 1920;
	private const int ScreenHeight = 1080;
	private const int Precision = 3;

	[Fact]
	public void TheCornerSitsDownRightOfThePointer()
	{
		var corner = OnlineUiPanelPlacement.ForPointer(500f, 400f, 240f, 300f, ScreenWidth, ScreenHeight);

		Assert.Equal(500f + OnlineUiPanelPlacement.PointerOffsetX, corner.X, Precision);
		Assert.Equal(400f + OnlineUiPanelPlacement.PointerOffsetY, corner.Top, Precision);
	}

	[Fact]
	public void TheCornerKeepsThePanelInsideTheRightEdge()
	{
		var corner = OnlineUiPanelPlacement.ForPointer(
			ScreenWidth - 10f,
			400f,
			240f,
			300f,
			ScreenWidth,
			ScreenHeight);

		Assert.Equal(ScreenWidth - 240f - OnlineUiPanelPlacement.ScreenMargin, corner.X, Precision);
	}

	[Fact]
	public void TheCornerKeepsThePanelInsideTheBottomEdge()
	{
		var corner = OnlineUiPanelPlacement.ForPointer(
			500f,
			OnlineUiPanelPlacement.ScreenMargin,
			240f,
			300f,
			ScreenWidth,
			ScreenHeight);

		Assert.Equal(300f + OnlineUiPanelPlacement.ScreenMargin, corner.Top, Precision);
	}

	[Fact]
	public void TheCornerKeepsThePanelInsideTheTopEdge()
	{
		var corner = OnlineUiPanelPlacement.ForPointer(500f, ScreenHeight - 4f, 240f, 300f, ScreenWidth, ScreenHeight);

		Assert.Equal(ScreenHeight - OnlineUiPanelPlacement.ScreenMargin, corner.Top, Precision);
	}

	/// <summary>A panel too tall for the screen keeps its top on screen: its title and first actions are what
	/// the player needs to see, so the overflow goes off the bottom.</summary>
	[Fact]
	public void APanelTallerThanTheScreenHangsFromTheTop()
	{
		var corner = OnlineUiPanelPlacement.ForPointer(500f, 500f, 240f, ScreenHeight + 200f, ScreenWidth, ScreenHeight);

		Assert.Equal(ScreenHeight - OnlineUiPanelPlacement.ScreenMargin, corner.Top, Precision);
	}

	/// <summary>A panel wider than the screen keeps its left edge on screen rather than flipping to the far
	/// side (the empty-range case: the minimum wins).</summary>
	[Fact]
	public void APanelWiderThanTheScreenKeepsItsLeftEdgeOnScreen()
	{
		var corner = OnlineUiPanelPlacement.ForPointer(500f, 500f, ScreenWidth + 200f, 300f, ScreenWidth, ScreenHeight);

		Assert.Equal(OnlineUiPanelPlacement.ScreenMargin, corner.X, Precision);
	}

	/// <summary>The pointer may be outside the screen (a click at the very edge, or a resolution change
	/// between the click and the frame the menu is built in) — the answer stays inside it.</summary>
	[Fact]
	public void APointerOutsideTheScreenStillPlacesThePanelInside()
	{
		var corner = OnlineUiPanelPlacement.ForPointer(-50f, -50f, 240f, 300f, ScreenWidth, ScreenHeight);

		Assert.Equal(OnlineUiPanelPlacement.ScreenMargin, corner.X, Precision);
		Assert.Equal(300f + OnlineUiPanelPlacement.ScreenMargin, corner.Top, Precision);
	}
}
