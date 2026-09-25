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
/// drain tick runs every frame, so every frame is its own intent.
///
/// It holds no Unity types on purpose: the decision logic is testable without a
/// scene (the Game Adapter resolves the ids and asks the window, then skips the
/// original call when the window took it).
/// </summary>
internal sealed class RemoteDragIntentCapture
{
	private readonly List<RemoteDragIntent> _intents = [];
	private readonly List<string> _refusals = [];

	/// <summary>
	/// The container calls captured so far, in native call order. A pending entry
	/// is the `unload` half of the native pair: it becomes a
	/// <see cref="RemoteInventoryIntentKind.MoveIntoContainer"/> when the matching
	/// `load` arrives for the same container, and a
	/// <see cref="RemoteInventoryIntentKind.TakeOutOfContainer"/> when the bracket
	/// closes without one. The list — not a single slot — is what keeps a release
	/// that takes an item out of one container and puts it into another (W1 + W4 in
	/// one `TryPerformWorldActions`, `PlayerCamera.cs:1686`) as two intents in the
	/// native order.
	/// </summary>
	private readonly List<PendingContainerCall> _containerCalls = [];

	/// <summary>The children the container-expansion loop (R5) unloaded out of the dragged item's own container inside this bracket.</summary>
	private readonly HashSet<ulong> _batchChildren = [];

	private bool _heldItemDropped;
	private bool _slotPickUpSeen;
	private RemoteDragNoOp _noOp;
	private RemoteDragWindowKind _kind;
	private ulong _batchTargetContainerId;
	private bool _batchTargetConflict;

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
		_containerCalls.Clear();
		_batchChildren.Clear();
		_batchTargetContainerId = 0;
		_batchTargetConflict = false;
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
	internal void CaptureContainerUnload(ulong itemInstanceId, ulong containerInstanceId)
	{
		if (itemInstanceId == DraggedItemId)
		{
			_containerCalls.Add(new PendingContainerCall(itemInstanceId, containerInstanceId, Pending: true));
			return;
		}

		// R5's per-child loop unloads each direct child out of the dragged item's
		// OWN container (the native source is `dragItem.container`,
		// PlayerCamera.cs:1589), so an unload whose source is the dragged item is the
		// batch's first half. The child identities are kept only to match the load
		// that follows: the intent carries the dragged item, and the owner enumerates
		// the children on the real objects.
		if (containerInstanceId == DraggedItemId)
		{
			_batchChildren.Add(itemInstanceId);
			return;
		}

		Refuse($"container unload of item {itemInstanceId} from container {containerInstanceId}, which is neither the dragged proxy itself nor its own container");
	}

	/// <summary>
	/// <c>Container.LoadItem(item)</c> — the second half of the native move pair for
	/// the same container, or of one container-expansion iteration.
	/// </summary>
	internal void CaptureContainerLoad(ulong itemInstanceId, ulong containerInstanceId)
	{
		if (itemInstanceId == DraggedItemId)
		{
			// The matching pending unload on THIS container makes the pair one move;
			// an unload still pending on another container stays pending and closes as
			// its own take-out, which is the native order of a release that takes an
			// item out of one container and drops it into another (W1 + W4).
			for (var index = _containerCalls.Count - 1; index >= 0; index--)
			{
				var pending = _containerCalls[index];
				if (pending.Pending && pending.ContainerInstanceId == containerInstanceId)
				{
					_containerCalls[index] = pending with { Pending = false };
					return;
				}
			}

			_containerCalls.Add(new PendingContainerCall(itemInstanceId, containerInstanceId, Pending: false));
			return;
		}

		// The second half of one R5 iteration: a child this bracket already saw
		// unloaded out of the dragged container now loads into the hit container.
		// Every child of that loop shares the target, so the first completed pair
		// names the ONE intent the gesture is and the remaining pairs are the same
		// gesture — one intent per child would make the owner run the loop once per
		// child on a container the first run already emptied.
		if (_batchChildren.Contains(itemInstanceId) && containerInstanceId != DraggedItemId)
		{
			if (_batchTargetContainerId == 0)
			{
				_batchTargetContainerId = containerInstanceId;
			}
			else if (_batchTargetContainerId != containerInstanceId)
			{
				// The native loop resolves its target container once per invocation, so
				// two targets inside one bracket is not a gesture this vocabulary can
				// name: the batch is refused whole rather than sent against the first
				// target. (Defensive — no native path produces it.)
				_batchTargetConflict = true;
				Refuse($"container-expansion batch reaching two targets ({_batchTargetContainerId} and {containerInstanceId})");
			}

			return;
		}

		Refuse($"container load of item {itemInstanceId} into container {containerInstanceId}, which is neither the dragged proxy itself nor one of its unloaded children");
	}

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
		_containerCalls.Clear();
		_batchChildren.Clear();
		_batchTargetContainerId = 0;
		_batchTargetConflict = false;
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
		if (_batchTargetConflict)
		{
			// The refusal already names both targets; a half-applied batch would be a
			// gesture the native loop never makes.
		}
		else if (_batchTargetContainerId != 0)
		{
			Add(RemoteInventoryIntentKind.MoveContainerChildren, DraggedItemId, _batchTargetContainerId, -1, 0, -1);
		}
		else if (_batchChildren.Count > 0)
		{
			Refuse($"container-expansion unload of {_batchChildren.Count} child item(s) with no load behind it — the native loop never leaves a child unloaded, so this gesture is not one the vocabulary can name");
		}

		foreach (var call in _containerCalls)
		{
			if (call.Pending)
			{
				Add(RemoteInventoryIntentKind.TakeOutOfContainer, call.ItemInstanceId, call.ContainerInstanceId, -1, 0, -1);
				continue;
			}

			Add(RemoteInventoryIntentKind.MoveIntoContainer, call.ItemInstanceId, call.ContainerInstanceId, -1, 0, -1);
		}

		if (_heldItemDropped && !_slotPickUpSeen)
		{
			Add(RemoteInventoryIntentKind.DropItem, DraggedItemId, 0, -1, 0, -1);
		}
	}

	/// <summary>
	/// A while-dragging frame carries the continuous native calls and nothing else.
	/// A release-only capture reaching it would be a gesture that frame cannot
	/// produce, so it is refused and its intent is dropped rather than sent as
	/// something the native body never did.
	/// </summary>
	private void RefuseReleaseCallsInAWhileDraggingFrame()
	{
		if (_containerCalls.Count == 0 && _batchChildren.Count == 0 && !_heldItemDropped && !_slotPickUpSeen)
		{
			return;
		}

		Refuse("a release-only mutation call inside a while-dragging frame");
		_intents.RemoveAll(intent => !intent.Kind.IsContinuousGesture());
	}

	private void Add(RemoteInventoryIntentKind kind, ulong itemInstanceId, ulong containerInstanceId, int slotIndex, ulong bodySteamId, int limbIndex, float amount = 0f) =>
		_intents.Add(new RemoteDragIntent(kind, itemInstanceId, containerInstanceId, slotIndex, bodySteamId, limbIndex) { Amount = amount });

	/// <summary>One captured container call: pending until its `load` on the same container turns it into a move.</summary>
	private readonly record struct PendingContainerCall(ulong ItemInstanceId, ulong ContainerInstanceId, bool Pending);
}
