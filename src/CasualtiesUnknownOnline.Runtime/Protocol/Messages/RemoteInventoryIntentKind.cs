namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One native inventory mutation the game's own drag pipeline performed on a
/// remote player's display proxy, named by the native call the owner must
/// replay. The vocabulary is the native call identity, never a CUO semantic
/// taxonomy: mode (move, swap, drop, take-out, transfer) follows from which
/// call the native branch made, not from a routing table.
/// </summary>
public enum RemoteInventoryIntentKind
{
	/// <summary><c>Body.DropItem(item)</c> — the held item leaves the body into the world (R9's first step, W2).</summary>
	DropItem = 1,

	/// <summary><c>Body.DropWearable(item)</c> — the worn item is removed and dropped (W3).</summary>
	DropWearable = 2,

	/// <summary><c>Container.UnloadItem(item, null)</c> with no load on that container — the item leaves its container into the world (W1).</summary>
	TakeOutOfContainer = 3,

	/// <summary><c>Container.UnloadItem(item, null)</c> immediately followed by <c>Container.LoadItem(item)</c> on the same container — one move into that container (R4, R13, W4).</summary>
	MoveIntoContainer = 4,

	/// <summary><c>Body.SwapSlots(slot, body.SlotOf(item))</c> — the two slots exchange their items (R8).</summary>
	SwapSlots = 5,

	/// <summary>R9's slot release: <c>Body.PickUpItem(item, slot, false)</c> with the native guards; the owner resolves the item's own home while replaying it.</summary>
	PickUpToSlot = 6,

	/// <summary>
	/// The two-sided custody transfer of R9 made across players: the owner
	/// releases the item and the destination body runs R9's own sequence with
	/// the requester's body as the destination. The host arbitrates the item
	/// id between the two halves; no display proxy is mutated.
	/// </summary>
	TransferToBody = 7,

	/// <summary><c>PlayerCamera.ApplyWoundItem(item)</c> — the dragged item is applied to a limb of the acting body (R11); the owner consumes/updates the item.</summary>
	ApplyToLimb = 8,
}
