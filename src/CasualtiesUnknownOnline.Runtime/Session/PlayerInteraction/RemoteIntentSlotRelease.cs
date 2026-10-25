using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The decision half of a slot intent, kept pure so it can be covered without a
/// scene: which native steps a slot release runs, in which order, and whether the
/// destination slot is one the body can even be indexed with.
///
/// <c>Body.PickUpItem</c>, <c>Body.HoldingItem(int)</c>, <c>Body.GetItem(int)</c>
/// and <c>Body.DropItem(int)</c> all index <c>slots[slot]</c> without a bounds
/// check, so an out-of-range slot from the wire must be refused before the
/// owner's body is touched.
/// </summary>
internal static class RemoteIntentSlotRelease
{
	/// <summary>Whether the slot is one of the body's own.</summary>
	internal static bool IsValidSlot(int slotCount, int slot) =>
		slot >= 0 && slot < slotCount;

	/// <summary>
	/// R9 exactly as the game runs it for a release onto a BODY slot, both branches — <c>PlayerCamera.cs</c>'s own body-slot release:
	///
	/// <code>
	/// if (this.body.HoldingItem(invButton.slot) &amp;&amp; this.body.HoldingItem(this.dragItem)) {
	///     this.body.SwapSlots(invButton.slot, this.body.SlotOf(this.dragItem));
	/// } else {
	///     if (this.body.HoldingItem(this.dragItem)) { this.body.DropItem(this.dragItem); }
	///     if (this.body.HoldingItem(invButton.slot)) { this.body.DropItem(invButton.slot); }
	///     this.body.PickUpItem(this.dragItem, invButton.slot, false);
	/// }
	/// </code>
	///
	/// The both-held branch is a SWAP and not a drop-then-pick: there, the drop-pick pair drops the item occupying the destination and then
	/// picks up ONLY the dragged one, so the occupant is left lying on the ground — the user's "gave an item, then rearranged it on that player,
	/// and the item was swallowed"
	/// (2026-10-25, five rearranges in the host's log, each of them onto the slot the freshly given item sat in). CUO had applied the
	/// else-branch sequence to both cases, which is why the item that vanished was always the one that had just been given while an item moved
	/// onto an EMPTY slot was safe.
	///
	/// An invalid slot produces no steps at all.
	/// </summary>
	internal static IReadOnlyList<RemoteIntentStep> Plan(int slotCount, int targetSlot, bool itemIsHeld, bool slotIsHeld)
	{
		if (!IsValidSlot(slotCount, targetSlot))
		{
			return [];
		}

		if (itemIsHeld && slotIsHeld)
		{
			return [RemoteIntentStep.MoveBySwap];
		}

		var steps = new List<RemoteIntentStep>(3);
		if (itemIsHeld)
		{
			steps.Add(RemoteIntentStep.DropHeldItem);
		}

		if (slotIsHeld)
		{
			steps.Add(RemoteIntentStep.DropSlotItem);
		}

		steps.Add(RemoteIntentStep.PickUp);
		return steps;
	}
}
