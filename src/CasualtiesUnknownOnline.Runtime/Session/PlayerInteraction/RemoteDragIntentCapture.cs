using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The native drag-window state machine, in two kinds. The game's own drag
/// pipeline runs on the viewer's client, and a window spans exactly ONE native
/// invocation — a call bracket, not a time bracket — so the only calls inside it
/// are the ones the native method itself makes. A RELEASE window spans one
/// <c>PlayerCamera.HandleReleaseDragging</c> invocation: the native mutation entry
/// points report the call the branch made, the window turns those calls back into
/// the native call identity the owner must replay, coalesces the two shapes the
/// native code expresses as a pair, and records everything it could not name so a
/// gesture CUO has never seen is observable instead of a silent no-op. A
/// WHILE-DRAGGING window spans one <c>PlayerCamera.HandleWhileDragging</c> frame,
/// which carries the continuous actions rather than a release branch — the liquid
/// drain tick runs every frame, so every frame is its own intent — plus the one
/// mutation with no native call behind it: the <c>favourited</c> field store on the
/// hovered item, which the Game Adapter reports here after comparing that field
/// across the bracket (<see cref="CaptureFavourite"/>). The item-interaction half of
/// the vocabulary — R10's use and wear, R6's combine, R2/R3's battery calls, the
/// favourite store and R12's trader hand-in — lives on the same state, in
/// <c>RemoteDragIntentCapture.ItemInteractions.cs</c>.
///
/// It holds no Unity types on purpose: the decision logic is testable without a
/// scene (the Game Adapter resolves the ids and asks the window, then skips the
/// original call when the window took it).
/// </summary>
internal sealed partial class RemoteDragIntentCapture
{
	private readonly List<RemoteDragIntent> _intents = [];
	private readonly List<string> _refusals = [];

	/// <summary>The container-call pairing rules of this bracket, in their own state machine.</summary>
	private readonly RemoteContainerMoveCapture _containerMoves = new();

	private bool _heldItemDropped;
	private bool _slotPickUpSeen;
	private RemoteDragNoOp _noOp;
	private RemoteDragWindowKind _kind;

	internal bool IsOpen { get; private set; }

	/// <summary>The display proxy the window was opened for.</summary>
	internal ulong DraggedItemId { get; private set; }

	/// <summary>The player whose real items the captured calls belong to.</summary>
	internal ulong OwnerSteamId { get; private set; }

	internal ulong LocalSteamId { get; private set; }

	/// <summary>
	/// True while the native inventory ring shows the OWNER's displayed body
	/// (every <c>InvButton</c> reads its slots from the focused clone), so a slot
	/// call names the owner's body. False once the remote view is closed: the ring
	/// is the local body again and a slot call names the requester, which is
	/// <see cref="RemoteInventoryIntentKind.TransferToBody"/>.
	/// </summary>
	internal bool DestinationIsOwnerBody { get; private set; }

	/// <summary>Open a release bracket: one <c>PlayerCamera.HandleReleaseDragging</c> invocation, whose calls become the discrete intents.</summary>
	internal void Open(ulong draggedItemId, ulong ownerSteamId, ulong localSteamId, bool destinationIsOwnerBody, RemoteDragNoOp noOp)
	{
		Reset(draggedItemId, ownerSteamId, localSteamId, destinationIsOwnerBody, noOp);
		_kind = RemoteDragWindowKind.Release;
	}

	/// <summary>
	/// Open a while-dragging bracket: one <c>PlayerCamera.HandleWhileDragging</c>
	/// frame, whose only native mutations are the continuous ones the local
	/// gesture produces every frame (the liquid drain tick). The bracket opens for
	/// a proxy drag only, so the ring shows the owner's displayed body here too and
	/// a predicate the frame reads is answered from that body.
	/// </summary>
	internal void OpenWhileDragging(ulong draggedItemId, ulong ownerSteamId)
	{
		Reset(draggedItemId, ownerSteamId, 0, destinationIsOwnerBody: true, RemoteDragNoOp.None);
		_kind = RemoteDragWindowKind.WhileDragging;
	}

	private void Reset(ulong draggedItemId, ulong ownerSteamId, ulong localSteamId, bool destinationIsOwnerBody, RemoteDragNoOp noOp)
	{
		_intents.Clear();
		_refusals.Clear();
		_containerMoves.Reset(draggedItemId, Refuse);
		_heldItemDropped = false;
		_slotPickUpSeen = false;
		DraggedItemId = draggedItemId;
		OwnerSteamId = ownerSteamId;
		LocalSteamId = localSteamId;
		DestinationIsOwnerBody = destinationIsOwnerBody;
		_noOp = noOp;
		IsOpen = true;
	}

	/// <summary>
	/// Whether the native release branch's own guards must be answered by the body
	/// the inventory ring is showing instead of the body the call was made on.
	/// True only while a bracket is open on the owner's ring and the call is about
	/// the local body (the ring is the displayed clone's, `PlayerCamera.body` is
	/// always the local body, so without this answer the branch's own guards
	/// evaluate against the wrong body). The adapter's predicate patches and the
	/// applier's body resolution both go through this one decision.
	/// </summary>
	internal bool ShouldAnswerFromDisplayBody(bool isLocalBody) =>
		IsOpen && DestinationIsOwnerBody && isLocalBody;

	/// <summary>
	/// Whether the native drag pipeline's own pickup-feasibility gate
	/// (<c>Body.DoPickupCheck</c>) must be answered with "yes" for this item instead
	/// of being evaluated against the local scene. True for the dragged display proxy
	/// inside EITHER bracket kind: the gate is a world-space distance/line-of-sight
	/// test between the LOCAL body and the item, and a proxy stands where the remote
	/// player stands, so the native answer is a refusal the gesture never earned — in
	/// a release bracket it would drop the whole release before any branch ran, and in
	/// a while-dragging frame it would silently drop that frame's drain tick. One
	/// decision, one place: the release branch and the while-dragging body read it
	/// through the same predicate patch.
	/// </summary>
	internal bool AnswersPickupCheckFor(ulong itemInstanceId) =>
		IsOpen && itemInstanceId != 0 && itemInstanceId == DraggedItemId;

	/// <summary>
	/// True when a CONTINUOUS native call from this item is inside a capturing
	/// while-dragging frame — the one state in which the drain tick becomes an intent.
	/// Anything else is a display proxy no bracket took (the proxy carries no
	/// authoritative identity, or the bracket belongs to another item), and its native
	/// call must be refused rather than run: a display proxy is never mutated.
	/// </summary>
	internal bool CapturesContinuousCallsFor(ulong itemInstanceId) =>
		_kind == RemoteDragWindowKind.WhileDragging && itemInstanceId != 0 && itemInstanceId == DraggedItemId;

	/// <summary><c>Body.DropItem(item)</c>: the held item leaves the body (R9's first step or W2).</summary>
	internal void CaptureDropItem(ulong itemInstanceId)
	{
		if (itemInstanceId != DraggedItemId)
		{
			Refuse($"drop of item {itemInstanceId}, which is not the dragged proxy");
			return;
		}

		// R9 drops the held item before picking it up again in another slot; the
		// drop is only a real release when no slot pick-up follows in the same
		// bracket. Deferred, absorbed by CapturePickUp.
		_heldItemDropped = true;
	}

	/// <summary><c>Body.DropWearable(item)</c> (W3).</summary>
	internal void CaptureDropWearable(ulong itemInstanceId)
	{
		if (itemInstanceId != DraggedItemId)
		{
			Refuse($"drop of wearable {itemInstanceId}, which is not the dragged proxy");
			return;
		}

		Add(RemoteInventoryIntentKind.DropWearable, itemInstanceId, 0, -1, 0, -1);
	}

	/// <summary><c>Body.SwapSlots(slot, body.SlotOf(item))</c> (R8); the owner resolves the item's own slot.</summary>
	internal void CaptureSwapSlots(ulong itemInstanceId, int targetSlot)
	{
		if (itemInstanceId != DraggedItemId)
		{
			Refuse($"slot swap of item {itemInstanceId}, which is not the dragged proxy");
			return;
		}

		_heldItemDropped = false;
		Add(RemoteInventoryIntentKind.SwapSlots, itemInstanceId, 0, targetSlot, 0, -1);
	}

	/// <summary><c>Body.PickUpItem(item, slot, false)</c>: R9's slot release, on the owner's body or the requester's.</summary>
	internal void CapturePickUp(ulong itemInstanceId, int targetSlot)
	{
		if (itemInstanceId != DraggedItemId)
		{
			Refuse($"slot pick-up of item {itemInstanceId}, which is not the dragged proxy");
			return;
		}

		_slotPickUpSeen = true;
		if (DestinationIsOwnerBody)
		{
			Add(RemoteInventoryIntentKind.PickUpToSlot, itemInstanceId, 0, targetSlot, 0, -1);
			return;
		}

		Add(RemoteInventoryIntentKind.TransferToBody, itemInstanceId, 0, targetSlot, LocalSteamId, -1);
	}

	/// <summary>
	/// <c>Container.UnloadItem(item, null)</c> — half of the native move pair, the
	/// first half of one container-expansion iteration (R5), or a take into the
	/// world on its own.
	/// </summary>
	internal void CaptureContainerUnload(ulong itemInstanceId, ulong containerInstanceId) =>
		_containerMoves.Unload(itemInstanceId, containerInstanceId);

	/// <summary>
	/// <c>Container.LoadItem(item)</c> — the second half of the native move pair for
	/// the same container, or of one container-expansion iteration.
	/// </summary>
	internal void CaptureContainerLoad(ulong itemInstanceId, ulong containerInstanceId) =>
		_containerMoves.Load(itemInstanceId, containerInstanceId);

	/// <summary><c>PlayerCamera.ApplyWoundItem</c>: the dragged item is applied to a limb (R11).</summary>
	internal void CaptureApplyToLimb(ulong itemInstanceId, int limbIndex)
	{
		if (itemInstanceId != DraggedItemId)
		{
			Refuse($"limb application of item {itemInstanceId}, which is not the dragged proxy");
			return;
		}

		Add(RemoteInventoryIntentKind.ApplyToLimb, itemInstanceId, 0, -1, 0, limbIndex);
	}

	/// <summary>
	/// <c>WaterContainerItem.Drain</c>: the while-dragging liquid tick
	/// (<c>PlayerCamera.cs:1732</c>) ran on this frame. The native caller computed
	/// the per-stack list from the PROXY's stack; the intent carries the amount that
	/// list removed instead, and the owner re-derives the distribution from the
	/// stack its own item really has.
	/// </summary>
	internal void CaptureDrain(ulong itemInstanceId, float amount)
	{
		if (_kind != RemoteDragWindowKind.WhileDragging)
		{
			Refuse($"liquid drain of item {itemInstanceId} outside a while-dragging frame");
			return;
		}

		if (itemInstanceId != DraggedItemId)
		{
			Refuse($"liquid drain of item {itemInstanceId}, which is not the dragged proxy");
			return;
		}

		if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
		{
			Refuse($"liquid drain with an unusable amount ({amount})");
			return;
		}

		Add(RemoteInventoryIntentKind.Drain, itemInstanceId, 0, -1, 0, -1, amount);
	}

	/// <summary>Record a native call this stage's vocabulary cannot carry — the call is skipped, never run on a proxy.</summary>
	internal void Refuse(string what) => _refusals.Add(what);

	/// <summary>
	/// Close the bracket and produce the outcome. A container-expansion batch
	/// becomes its ONE intent — or one refusal when its children were unloaded with
	/// nothing loading them, a shape the native loop never produces. Each container
	/// call still pending is a take into the world (W1); a held-item drop with no
	/// slot pick-up behind it is a world drop (W2). Everything is appended in native
	/// call order, so a take-out followed by a move into another container stays two
	/// intents in that order.
	/// </summary>
	internal RemoteDragOutcome Close()
	{
		if (_kind == RemoteDragWindowKind.WhileDragging)
		{
			RefuseReleaseCallsInAWhileDraggingFrame();
		}
		else
		{
			AppendReleaseIntents();
		}

		var outcome = new RemoteDragOutcome([.. _intents], [.. _refusals], DraggedItemId, OwnerSteamId, _noOp, _kind);
		_containerMoves.Reset(0, Refuse);
		_heldItemDropped = false;
		_slotPickUpSeen = false;
		_noOp = RemoteDragNoOp.None;
		IsOpen = false;
		DraggedItemId = 0;
		OwnerSteamId = 0;
		DestinationIsOwnerBody = false;
		return outcome;
	}

	/// <summary>Emit what the native release branch did, in native call order.</summary>
	private void AppendReleaseIntents()
	{
		_containerMoves.AppendIntents(_intents);

		if (_heldItemDropped && !_slotPickUpSeen)
		{
			Add(RemoteInventoryIntentKind.DropItem, DraggedItemId, 0, -1, 0, -1);
		}
	}

	/// <summary>
	/// A while-dragging frame carries the continuous native calls and the field-store
	/// seam, and nothing else. A release-only capture reaching it would be a gesture
	/// that frame cannot produce, so it is refused and its intent is dropped rather
	/// than sent as something the native body never did — while the frame's OWN kinds
	/// (the drain tick, the favourite store) are kept.
	/// </summary>
	private void RefuseReleaseCallsInAWhileDraggingFrame()
	{
		if (!_containerMoves.HasCalls && !_heldItemDropped && !_slotPickUpSeen)
		{
			return;
		}

		Refuse("a release-only mutation call inside a while-dragging frame");
		_intents.RemoveAll(intent => !intent.Kind.IsWhileDraggingGesture());
	}

	private void Add(
		RemoteInventoryIntentKind kind,
		ulong itemInstanceId,
		ulong containerInstanceId,
		int slotIndex,
		ulong bodySteamId,
		int limbIndex,
		float amount = 0f,
		ulong targetItemInstanceId = 0,
		NetVector2Msg? targetTraderPosition = null) =>
		_intents.Add(new RemoteDragIntent(kind, itemInstanceId, containerInstanceId, slotIndex, bodySteamId, limbIndex)
		{
			Amount = amount,
			TargetItemInstanceId = targetItemInstanceId,
			TargetTraderPosition = targetTraderPosition,
		});
}
