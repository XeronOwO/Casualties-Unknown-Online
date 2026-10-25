namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>One step of the native call the owner replays, in native order.</summary>
internal enum RemoteIntentStep
{
	/// <summary>R9's first step: the held item leaves its slot (<c>Body.DropItem(item)</c>).</summary>
	DropHeldItem = 1,

	/// <summary>R9's occupying-slot step: the item holding the destination slot leaves it (<c>Body.DropItem(slot)</c>).</summary>
	DropSlotItem = 2,

	/// <summary><c>Body.PickUpItem(item, slot, false)</c> — the game's own guards decide.</summary>
	PickUp = 3,

	/// <summary>
	/// <c>Body.SwapSlots(targetSlot, body.SlotOf(item))</c> — the game's own call for a release onto a slot that is ALREADY holding an item, and
	/// the only branch that keeps BOTH items: <c>Body.SwapSlots</c> drops the two, picks the dragged one into the destination and picks the
	/// occupant back into the dragged item's old slot. The drop-drop-pick sequence CUO used instead picks up only the dragged item and leaves the
	/// occupant on the ground — the item the player had just been given (see RemoteIntentSlotRelease's own note).
	/// </summary>
	MoveBySwap = 4,
}
