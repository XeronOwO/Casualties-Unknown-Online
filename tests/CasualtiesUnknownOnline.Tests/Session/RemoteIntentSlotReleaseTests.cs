using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Behavior family: the pure decision half of a slot intent — which native steps
/// R9 runs, in which order, and which slots the owner's body may be indexed with.
/// The whole point of the type is that it is covered without a scene: the game's
/// <c>Body.PickUpItem</c>, <c>HoldingItem(int)</c>, <c>GetItem(int)</c> and
/// <c>DropItem(int)</c> index <c>slots[slot]</c> without a bounds check, so this
/// decision runs before any body is touched.
/// </summary>
public class RemoteIntentSlotReleaseTests
{
	[Theory]
	[InlineData(3, 0, true)]
	[InlineData(3, 2, true)]
	[InlineData(3, 3, false)]
	[InlineData(3, -1, false)]
	[InlineData(0, 0, false)]
	public void IsValidSlot_MatchesTheBodysOwnSlots(int slotCount, int slot, bool expected) =>
		Assert.Equal(expected, RemoteIntentSlotRelease.IsValidSlot(slotCount, slot));

	[Fact]
	public void WithoutAHandOrAnOccupant_TheReleaseIsJustThePickUp()
	{
		var steps = RemoteIntentSlotRelease.Plan(slotCount: 3, targetSlot: 1, itemIsHeld: false, slotIsHeld: false);

		Assert.Equal([RemoteIntentStep.PickUp], steps);
	}

	[Fact]
	public void ACarriedItem_ChangesSlotsByASwapAndNeverTouchesTheWorld()
	{
		// The native drop-then-pick pair makes a carried item a WORLD item in between: its drop report reaches the host, the row can be retired,
		// and a re-pick the native guards refuse strands it on the ground (the "换完格子物品被吞了" report). A swap moves it in place.
		var steps = RemoteIntentSlotRelease.Plan(slotCount: 3, targetSlot: 1, itemIsHeld: true, slotIsHeld: false);

		Assert.Equal([RemoteIntentStep.MoveBySwap], steps);
	}

	[Fact]
	public void ACarriedItem_IntoAnOccupiedSlot_IsStillOneSwap()
	{
		// A swap exchanges the two, which is the same outcome the drop-drop-pick trio produced in three world-visible steps.
		var steps = RemoteIntentSlotRelease.Plan(slotCount: 3, targetSlot: 2, itemIsHeld: true, slotIsHeld: true);

		Assert.Equal([RemoteIntentStep.MoveBySwap], steps);
	}

	[Fact]
	public void AWorldItem_IntoAnOccupiedSlot_FreesTheOccupantBeforeThePickUp()
	{
		// A world item has no slot to swap FROM, so the native order stands: the occupying item leaves, then the pickup runs with the guards.
		var steps = RemoteIntentSlotRelease.Plan(slotCount: 3, targetSlot: 2, itemIsHeld: false, slotIsHeld: true);

		Assert.Equal([RemoteIntentStep.DropSlotItem, RemoteIntentStep.PickUp], steps);
	}

	[Fact]
	public void AnInvalidSlot_PlansNothingAtAll()
	{
		Assert.Empty(RemoteIntentSlotRelease.Plan(slotCount: 3, targetSlot: 7, itemIsHeld: true, slotIsHeld: true));
		Assert.Empty(RemoteIntentSlotRelease.Plan(slotCount: 3, targetSlot: -1, itemIsHeld: false, slotIsHeld: false));
	}
}
