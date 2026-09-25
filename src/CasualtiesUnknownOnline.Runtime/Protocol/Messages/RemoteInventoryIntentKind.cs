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

	/// <summary>
	/// R5's per-child loop (<c>PlayerCamera.cs:1585</c>): the dragged item's OWN
	/// container is emptied into the hit container, one direct child at a time,
	/// with the native <c>Container.CanHoldItem</c> gate in front of each pair. The
	/// dragged item is the operand because the native source of the children is
	/// <c>dragItem.container</c> — the item's own container component — so the owner
	/// enumerates the children on the real objects and decides there which of them
	/// fit, never on the viewer's projection.
	/// </summary>
	MoveContainerChildren = 9,

	/// <summary>
	/// <c>WaterContainerItem.Drain</c> — the while-dragging liquid drain tick
	/// (<c>PlayerCamera.cs:1729</c> is its gate, <c>:1731</c> the call), which the
	/// native code runs every frame with
	/// <c>CalculateDrain(0.2f * Time.deltaTime * Capacity)</c>. The operand is the
	/// drained AMOUNT, not the per-stack list the native caller computed: the owner
	/// re-derives the distribution from its own stack, so the amount is the
	/// semantic both sides share and a stale stack on the viewer cannot leak into
	/// the owner's item.
	/// </summary>
	Drain = 10,

	/// <summary>
	/// <c>Body.UseItem(item)</c> — the radial-centre use branch (R10,
	/// <c>PlayerCamera.cs:1646</c>): the dragged item is used on the body the
	/// inventory ring shows, which while the remote view is open is the OWNER's
	/// displayed body, so the owner's client is where the native use action runs.
	/// The branch's two <c>if</c>s are independent, so one release can produce this
	/// kind and <see cref="WearItem"/> together.
	/// </summary>
	UseItem = 11,

	/// <summary><c>Body.WearWearable(item)</c> — the radial-centre wear branch (R10, <c>PlayerCamera.cs:1642</c>), the same body and the same pair rule as <see cref="UseItem"/>.</summary>
	WearItem = 12,

	/// <summary>
	/// <c>Body.CombineItems(target, item)</c> (R6, <c>PlayerCamera.cs:1602</c>).
	/// The native call takes two items and the FIRST one is the receiver (a gun
	/// takes the magazine, a container with space takes the liquid, the condition
	/// merge credits <c>it1</c>), so the intent names both: the dragged item in
	/// <c>ItemInstanceId</c> and the hit item in <c>TargetItemInstanceId</c>.
	/// </summary>
	CombineItems = 13,

	/// <summary>
	/// <c>BatteryItem.LoadBattery(item)</c> (R3, <c>PlayerCamera.cs:1550</c>): the
	/// dragged battery item goes into the hit item's battery slot. Two operands —
	/// the dragged battery in <c>ItemInstanceId</c>, the receiving item in
	/// <c>TargetItemInstanceId</c> — because the native call consumes one and
	/// charges the other.
	/// </summary>
	LoadBattery = 14,

	/// <summary>
	/// <c>BatteryItem.UnloadBattery(false)</c> (R2, <c>PlayerCamera.cs:1545</c>): the
	/// HIT item's own battery is ejected (and auto-picked-up by the body that
	/// runs the call). One operand — the hit item, in <c>ItemInstanceId</c> — because
	/// the call reads nothing else; the dragged item only told the native dispatch
	/// which direction the gesture was.
	/// </summary>
	UnloadBattery = 15,

	/// <summary>
	/// The <c>favourited</c> field store on the HOVERED item (while-dragging,
	/// <c>PlayerCamera.cs:1747</c>). The native code has no call behind it, so the
	/// viewer detects the store by comparing the field across the frame bracket and
	/// sends a toggle; the owner flips its own real item and the projection carries
	/// the authoritative value back.
	/// </summary>
	ToggleFavourite = 16,

	/// <summary>
	/// <c>TraderScript.GiveItem(item)</c> (R12, <c>PlayerCamera.cs:1663</c>): the
	/// dragged item is handed to the trader the requester has open. The trader is an
	/// operand because the owner's client has no <c>PlayerCamera.currentTrader</c>
	/// open — the intent carries the trader's world position, the identity the trade
	/// domain already keys its messages by (<c>TraderSwingMsg.Position</c>,
	/// <c>TradeStateSync.FindTraderAt</c>).
	/// </summary>
	GiveToTrader = 17,
}
