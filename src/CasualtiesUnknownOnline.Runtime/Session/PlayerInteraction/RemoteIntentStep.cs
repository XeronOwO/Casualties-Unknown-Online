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
	/// A CARRIED item that only changes slots (<c>Body.SwapSlots(from, to)</c>). It replaces the drop-then-pick pair for that case because the
	/// drop makes the item a WORLD item for the length of the operation: its drop report reaches the host, the owner's row can be retired, and a
	/// pickup the native guards then refuse leaves the item lying in the world with nobody holding it — the "换完格子物品被吞了" report (guest
	/// log 2026-10-25: <c>OnItemDropped event=Drop</c> then <c>OnItemPickedUp result=Cancelled events=[RePick]</c>). A swap never leaves the
	/// body.
	/// </summary>
	MoveBySwap = 4,
}
