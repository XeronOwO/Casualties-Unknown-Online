using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// Which side of its field an open dropdown's list takes (ticket online-ui-layout-and-input-detail-pass, row 4;
/// the review of the sixth pass found the layer had cancelled the game's own flip). The rule is the game's own
/// answer — a list that would leave the canvas below flips above when there is room — decided from the canvas's
/// room instead of from a world-corner walk that only knows the layer for a parent.
/// </summary>
public sealed class OnlineUiDropdownPlacementTests
{
	private const float ListHeight = 193.4f;

	[Fact]
	public void AFittingListOpensBelow() =>
		Assert.Equal(
			OnlineUiDropdownPlacement.OpenSide.Below,
			OnlineUiDropdownPlacement.SideFor(ListHeight, roomBelow: ListHeight, roomAbove: 400f));

	[Fact]
	public void AListWithNoRoomBelowFlipsAboveWhenThereIsRoom() =>
		Assert.Equal(
			OnlineUiDropdownPlacement.OpenSide.Above,
			OnlineUiDropdownPlacement.SideFor(ListHeight, roomBelow: 100f, roomAbove: 400f));

	/// <summary>A field boxed in by the canvas keeps the side whose first option is still at the field: a
	/// half-covered list is the lesser evil than one whose beginning is off the screen.</summary>
	[Fact]
	public void AListThatFitsOnNeitherSideStaysBelow() =>
		Assert.Equal(
			OnlineUiDropdownPlacement.OpenSide.Below,
			OnlineUiDropdownPlacement.SideFor(ListHeight, roomBelow: 100f, roomAbove: 100f));

	/// <summary>An exact fit is a fit: the list's last pixel is on the canvas's edge.</summary>
	[Fact]
	public void AnExactFitOnEitherSideIsAFit()
	{
		Assert.Equal(
			OnlineUiDropdownPlacement.OpenSide.Below,
			OnlineUiDropdownPlacement.SideFor(ListHeight, roomBelow: ListHeight, roomAbove: 0f));
		Assert.Equal(
			OnlineUiDropdownPlacement.OpenSide.Above,
			OnlineUiDropdownPlacement.SideFor(ListHeight, roomBelow: 0f, roomAbove: ListHeight));
	}

	/// <summary>The anchor is the field's own point in the layer's coordinates: the layer is 1000×700 with its
	/// bottom-left at (−500, −350), the language field's centre at x −328 and its bottom at y −48, and the
	/// batch's live anchor was `0.172, 0.431`.</summary>
	[Fact]
	public void TheAnchorIsTheFieldsOwnPointInTheLayer()
	{
		Assert.Equal(0.172f, OnlineUiDropdownPlacement.AnchorX(fieldCenter: -328f, layerLeft: -500f, layerWidth: 1000f), 3);
		Assert.Equal(0.431f, OnlineUiDropdownPlacement.AnchorY(fieldEdge: -48f, layerBottom: -350f, layerHeight: 700f), 3);
	}

	/// <summary>A field dragged partly off the canvas puts its anchor outside `0..1`; the engine handles that
	/// exactly as it handles any anchor, so the rule must not clamp it away.</summary>
	[Fact]
	public void AFieldOutsideTheLayerKeepsItsAnchor()
	{
		Assert.Equal(1.1f, OnlineUiDropdownPlacement.AnchorX(fieldCenter: 600f, layerLeft: -500f, layerWidth: 1000f), 3);
		Assert.Equal(-0.1f, OnlineUiDropdownPlacement.AnchorY(fieldEdge: -420f, layerBottom: -350f, layerHeight: 700f), 3);
	}

	/// <summary>A layer that has not been laid out yet has no anchors to write; the rule answers its centre
	/// instead of dividing by zero, and the next frame's layer rect supplies the real one.</summary>
	[Fact]
	public void AnUnlaidLayerFallsBackToItsCentre()
	{
		Assert.Equal(0.5f, OnlineUiDropdownPlacement.AnchorX(fieldCenter: 0f, layerLeft: 0f, layerWidth: 0f));
		Assert.Equal(0.5f, OnlineUiDropdownPlacement.AnchorY(fieldEdge: 0f, layerBottom: 0f, layerHeight: 0f));
	}
}
