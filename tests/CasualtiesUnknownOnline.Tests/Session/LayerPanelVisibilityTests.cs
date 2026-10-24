using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The end-of-layer panel's visibility rule (ticket `layer-end-save-and-exit-native-write.md`, the user's 2026-10-24
/// ruling): the panel appears on the host when the share of LIVING players that finished the layer reaches the host
/// rule's threshold, and the host's own body's state does not enter the rule — a host who died is not one of the
/// living bodies counted, and its panel can still appear.
/// </summary>
[Trait("Category", "Unit")]
public class LayerPanelVisibilityTests
{
	// The default: every living player. Three living, three at the floor.
	[Fact]
	public void EveryoneLivingIsAtTheFloor_ShowsAtTheFullThreshold() =>
		Assert.True(LayerPanelVisibility.ShouldShow(livingBodies: 3, bodiesAtTheFloor: 3, requiredShare: 1f));

	[Fact]
	public void OneLivingPlayerShort_HidesAtTheFullThreshold() =>
		Assert.False(LayerPanelVisibility.ShouldShow(livingBodies: 3, bodiesAtTheFloor: 2, requiredShare: 1f));

	// The host's body is dead: it is not one of the living bodies, so two living members both at the floor meet the
	// full threshold and the host sees the panel.
	[Fact]
	public void ADeadHost_IsNotCounted_SoTwoOfTwoLivingStillShows() =>
		Assert.True(LayerPanelVisibility.ShouldShow(livingBodies: 2, bodiesAtTheFloor: 2, requiredShare: 1f));

	[Fact]
	public void AMajorityThreshold_ShowsBelowUnanimity()
	{
		Assert.True(LayerPanelVisibility.ShouldShow(livingBodies: 3, bodiesAtTheFloor: 2, requiredShare: 0.5f));
		Assert.False(LayerPanelVisibility.ShouldShow(livingBodies: 3, bodiesAtTheFloor: 1, requiredShare: 0.5f));
	}

	[Fact]
	public void NobodyAlive_ShowsNothing()
	{
		// A group with no living body has nobody to press the panel, so it stays hidden whatever the floor count says.
		Assert.False(LayerPanelVisibility.ShouldShow(livingBodies: 0, bodiesAtTheFloor: 0, requiredShare: 1f));
		Assert.False(LayerPanelVisibility.ShouldShow(livingBodies: 0, bodiesAtTheFloor: 0, requiredShare: 0f));
	}

	[Fact]
	public void AZeroThreshold_ShowsAsSoonAsAnybodyIsAlive() =>
		Assert.True(LayerPanelVisibility.ShouldShow(livingBodies: 2, bodiesAtTheFloor: 0, requiredShare: 0f));
}
