using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The release-window state machine. The game's own drag pipeline runs on the
/// viewer's client; this window is open for exactly one
/// <c>PlayerCamera.HandleReleaseDragging</c> invocation (a call bracket, not a
/// time bracket), and while it is open the native mutation entry points report
/// the call the native branch made. The window turns those calls back into the
/// native call identity the owner must replay, coalesces the two shapes the
/// native code expresses as a pair, and records everything it could not name so
/// a gesture CUO has never seen is observable instead of a silent no-op.
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

	private bool _heldItemDropped;
	private bool _slotPickUpSeen;
	private RemoteDragNoOp _noOp;
	private bool _containerBatchRefused;

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

	internal void Open(ulong draggedItemId, ulong ownerSteamId, ulong localSteamId, bool destinationIsOwnerBody, RemoteDragNoOp noOp)
	{
		_intents.Clear();
		_refusals.Clear();
		_containerCalls.Clear();
		_heldItemDropped = false;
		_slotPickUpSeen = false;
		_containerBatchRefused = false;
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

	/// <summary><c>Container.UnloadItem(item, null)</c> — half of the native move pair, or a take into the world on its own.</summary>
	internal void CaptureContainerUnload(ulong itemInstanceId, ulong containerInstanceId)
	{
		if (itemInstanceId != DraggedItemId)
		{
			// The native container-expansion gesture (R5) unloads each of the
			// dragged container's children: a multi-item batch whose intent the
			// owner evaluates itself, and which this stage does not carry yet.
			// One refusal for the whole batch — the loop calls twice per child and
			// ten lines naming one gesture would drown the signal.
			if (!_containerBatchRefused)
			{
				_containerBatchRefused = true;
				Refuse($"container-unload batch on child {itemInstanceId} of container {containerInstanceId} (multi-item container gesture)");
			}

			return;
		}

		_containerCalls.Add(new PendingContainerCall(itemInstanceId, containerInstanceId, Pending: true));
	}

	/// <summary><c>Container.LoadItem(item)</c> — the second half of the native move pair for the same container.</summary>
	internal void CaptureContainerLoad(ulong itemInstanceId, ulong containerInstanceId)
	{
		if (itemInstanceId != DraggedItemId)
		{
			if (!_containerBatchRefused)
			{
				_containerBatchRefused = true;
				Refuse($"container-load batch on child {itemInstanceId} into container {containerInstanceId} (multi-item container gesture)");
			}

			return;
		}

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

	/// <summary>Record a native call this stage's vocabulary cannot carry — the call is skipped, never run on a proxy.</summary>
	internal void Refuse(string what) => _refusals.Add(what);

	/// <summary>
	/// Close the bracket and produce the outcome. Each container call still
	/// pending is a take into the world (W1); a held-item drop with no slot
	/// pick-up behind it is a world drop (W2). Both are appended in native call
	/// order, so a take-out followed by a move into another container stays two
	/// intents in that order.
	/// </summary>
	internal RemoteDragOutcome Close()
	{
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

		var outcome = new RemoteDragOutcome([.. _intents], [.. _refusals], DraggedItemId, OwnerSteamId, _noOp);
		_containerCalls.Clear();
		_heldItemDropped = false;
		_slotPickUpSeen = false;
		_containerBatchRefused = false;
		_noOp = RemoteDragNoOp.None;
		IsOpen = false;
		DraggedItemId = 0;
		OwnerSteamId = 0;
		DestinationIsOwnerBody = false;
		return outcome;
	}

	private void Add(RemoteInventoryIntentKind kind, ulong itemInstanceId, ulong containerInstanceId, int slotIndex, ulong bodySteamId, int limbIndex) =>
		_intents.Add(new RemoteDragIntent(kind, itemInstanceId, containerInstanceId, slotIndex, bodySteamId, limbIndex));

	/// <summary>One captured container call: pending until its `load` on the same container turns it into a move.</summary>
	private readonly record struct PendingContainerCall(ulong ItemInstanceId, ulong ContainerInstanceId, bool Pending);
}
