using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The item-interaction half of the drag-window state machine: R10's radial-centre
/// use and wear, R6's combine, R2/R3's battery calls, the while-dragging
/// <c>favourited</c> store and R12's trader hand-in. They share the window's one
/// rule — the call the game's own branch made is named by its native identity and its
/// operands, and anything it cannot name is refused instead of run on a display proxy
/// — and they share its state (<see cref="DraggedItemId"/>, the owner, the bracket
/// kind), which is why they live on the same type in a second file rather than on a
/// second state machine that could disagree with the first.
/// </summary>
internal sealed partial class RemoteDragIntentCapture
{
	/// <summary><c>Body.UseItem(item)</c> — R10's radial-centre use branch.</summary>
	internal void CaptureUseItem(ulong itemInstanceId)
	{
		if (itemInstanceId != DraggedItemId)
		{
			Refuse($"use of item {itemInstanceId}, which is not the dragged proxy");
			return;
		}

		Add(RemoteInventoryIntentKind.UseItem, itemInstanceId, 0, -1, 0, -1);
	}

	/// <summary>
	/// <c>Body.WearWearable(item)</c> — R10's radial-centre wear branch. The native
	/// branch tests <c>wearable</c> and <c>usable</c> as two independent <c>if</c>s
	/// (<c>PlayerCamera.cs:1640-1647</c>), so an item carrying both flags produces
	/// this kind and <see cref="CaptureUseItem"/> from ONE release: the pair is the
	/// native order, not a coalescing rule.
	/// </summary>
	internal void CaptureWearItem(ulong itemInstanceId)
	{
		if (itemInstanceId != DraggedItemId)
		{
			Refuse($"wear of item {itemInstanceId}, which is not the dragged proxy");
			return;
		}

		Add(RemoteInventoryIntentKind.WearItem, itemInstanceId, 0, -1, 0, -1);
	}

	/// <summary>
	/// <c>Body.CombineItems(target, item)</c> (R6): the dragged item and the hit
	/// item. The second operand is the hit item, and it must belong to the SAME owner
	/// as the bracket — the ring's items are that owner's display proxies — so a
	/// combine that reaches across two owners is refused instead of being replayed on
	/// an item the intent may not name.
	/// </summary>
	internal void CaptureCombine(ulong itemInstanceId, ulong targetItemInstanceId, ulong targetOwnerSteamId)
	{
		if (itemInstanceId != DraggedItemId)
		{
			Refuse($"combine of item {itemInstanceId}, which is not the dragged proxy");
			return;
		}

		if (!IsOwnedTarget(targetItemInstanceId, targetOwnerSteamId, "combine"))
		{
			return;
		}

		Add(RemoteInventoryIntentKind.CombineItems, itemInstanceId, 0, -1, 0, -1, 0f, targetItemInstanceId);
	}

	/// <summary><c>BatteryItem.LoadBattery(item)</c> (R3): the dragged battery and the hit item that receives it.</summary>
	internal void CaptureBatteryLoad(ulong itemInstanceId, ulong targetItemInstanceId, ulong targetOwnerSteamId)
	{
		if (itemInstanceId != DraggedItemId)
		{
			Refuse($"battery load of item {itemInstanceId}, which is not the dragged proxy");
			return;
		}

		if (!IsOwnedTarget(targetItemInstanceId, targetOwnerSteamId, "battery load"))
		{
			return;
		}

		Add(RemoteInventoryIntentKind.LoadBattery, itemInstanceId, 0, -1, 0, -1, 0f, targetItemInstanceId);
	}

	/// <summary>
	/// <c>BatteryItem.UnloadBattery(false)</c> (R2): the call reads only the HIT
	/// item's own battery — the dragged item told the native dispatch the direction
	/// and nothing else — so the hit item is the single operand and takes the
	/// <c>ItemInstanceId</c> slot the host validates as the owner's.
	/// </summary>
	internal void CaptureBatteryUnload(ulong targetItemInstanceId, ulong targetOwnerSteamId)
	{
		if (!IsOwnedTarget(targetItemInstanceId, targetOwnerSteamId, "battery unload"))
		{
			return;
		}

		Add(RemoteInventoryIntentKind.UnloadBattery, targetItemInstanceId, 0, -1, 0, -1);
	}

	/// <summary>
	/// The while-dragging <c>favourited</c> store (<c>PlayerCamera.cs:1747</c>). The
	/// native code writes the FIELD of the HOVERED item — not the dragged one — and
	/// has no call to intercept, so the Game Adapter compares each candidate button's
	/// field across the frame bracket and reports a flip here. The flipped item must
	/// belong to the bracket's owner: the ring shows that owner's body, and a write
	/// that reached another owner's proxy would name an item the intent may not touch.
	/// </summary>
	internal void CaptureFavourite(ulong itemInstanceId, ulong itemOwnerSteamId)
	{
		if (_kind != RemoteDragWindowKind.WhileDragging)
		{
			Refuse($"favourite toggle of item {itemInstanceId} outside a while-dragging frame");
			return;
		}

		if (!IsOwnedTarget(itemInstanceId, itemOwnerSteamId, "favourite toggle"))
		{
			return;
		}

		Add(RemoteInventoryIntentKind.ToggleFavourite, itemInstanceId, 0, -1, 0, -1);
	}

	/// <summary>
	/// <c>TraderScript.GiveItem(item)</c> (R12). The trader is an operand because the
	/// owner's client has no trader open: the intent carries the position the trade
	/// domain already keys its messages by, and the owner resolves the trader its own
	/// scene has at that position.
	/// </summary>
	internal void CaptureGiveToTrader(ulong itemInstanceId, NetVector2Msg traderPosition)
	{
		if (itemInstanceId != DraggedItemId)
		{
			Refuse($"trader hand-in of item {itemInstanceId}, which is not the dragged proxy");
			return;
		}

		Add(RemoteInventoryIntentKind.GiveToTrader, itemInstanceId, 0, -1, 0, -1, 0f, 0, traderPosition);
	}

	/// <summary>
	/// Record a release outcome that is deliberately nothing, from the native branch's
	/// own answer. The release classifier names the no-ops it can read before the body
	/// runs (its own slot, a wearable that cannot be held, the craft button); this is
	/// for the one the body itself decides — the radial centre consuming a release for
	/// an item it has no action for (<c>PlayerCamera.cs:1638-1648</c>).
	/// </summary>
	internal void NoteClassifiedNoOp(RemoteDragNoOp noOp)
	{
		if (IsOpen && _kind == RemoteDragWindowKind.Release)
		{
			_noOp = noOp;
		}
	}

	/// <summary>
	/// A second item operand is only nameable when it carries an authoritative
	/// identity of the bracket's own owner: a proxy without an id can never be
	/// resolved on the owner's client, and another owner's item is outside what this
	/// bracket may touch. Both cases refuse, and the native call is skipped.
	/// </summary>
	private bool IsOwnedTarget(ulong targetItemInstanceId, ulong targetOwnerSteamId, string what)
	{
		if (targetItemInstanceId == 0)
		{
			Refuse($"{what} against an item without an authoritative instance id");
			return false;
		}

		if (targetOwnerSteamId != OwnerSteamId)
		{
			Refuse($"{what} against item {targetItemInstanceId}, which belongs to {targetOwnerSteamId} and not to {OwnerSteamId}");
			return false;
		}

		return true;
	}
}
