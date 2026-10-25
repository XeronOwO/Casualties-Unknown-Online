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
	public void AheldItem_OntoAnEmptySlot_LeavesItsSlotFirst()
	{
		var steps = RemoteIntentSlotRelease.Plan(slotCount: 3, targetSlot: 1, itemIsHeld: true, slotIsHeld: false);

		Assert.Equal([RemoteIntentStep.DropHeldItem, RemoteIntentStep.PickUp], steps);
	}

	[Fact]
	public void AheldItem_OntoAnOccupiedSlot_IsTheGamesOwnSwap()
	{
		// PlayerCamera's body-slot release: the both-held case is `Body.SwapSlots`, and it is the ONLY branch that keeps both items. The
		// drop-drop-pick sequence picks up the dragged item alone and strands the occupant on the ground — the item the player had just been
		// given, which is exactly the one that vanished in every report (2026-10-25).
		var steps = RemoteIntentSlotRelease.Plan(slotCount: 3, targetSlot: 2, itemIsHeld: true, slotIsHeld: true);

		Assert.Equal([RemoteIntentStep.MoveBySwap], steps);
	}

	[Fact]
	public void ACarriedItem_OntoAnOccupiedSlot_NeverDropsTheOccupant()
	{
		// The regression worth naming: the occupant of the destination may leave its slot only through the swap that returns it to the dragged
		// item's slot. A plan that drops it without a swap loses whatever the destination held — the freshly given item, in every report.
		var steps = RemoteIntentSlotRelease.Plan(slotCount: 3, targetSlot: 2, itemIsHeld: true, slotIsHeld: true);

		Assert.DoesNotContain(RemoteIntentStep.DropSlotItem, steps);
		Assert.Equal([RemoteIntentStep.MoveBySwap], steps);
	}

	[Fact]
	public void AnInvalidSlot_PlansNothingAtAll()
	{
		Assert.Empty(RemoteIntentSlotRelease.Plan(slotCount: 3, targetSlot: 7, itemIsHeld: true, slotIsHeld: true));
		Assert.Empty(RemoteIntentSlotRelease.Plan(slotCount: 3, targetSlot: -1, itemIsHeld: false, slotIsHeld: false));
	}
}
