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
	/// R9 in native order (<c>PlayerCamera.cs:1614-1629</c>): the held item leaves
	/// its slot, the item occupying the destination slot leaves its own when it is
	/// held, and then the pickup runs with the native guards — never forced.
	/// An invalid slot produces no steps at all.
	/// </summary>
	internal static IReadOnlyList<RemoteIntentStep> Plan(int slotCount, int targetSlot, bool itemIsHeld, bool slotIsHeld)
	{
		if (!IsValidSlot(slotCount, targetSlot))
		{
			return [];
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
