using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The container-call coalescing of one drag bracket. The native release expresses a
/// move as a PAIR — <c>Container.UnloadItem(item, null)</c> immediately followed by
/// <c>Container.LoadItem(item)</c> on the same container — and R5's expansion gesture
/// as a whole loop of such pairs, so this is the state machine that decides which
/// calls belong together: a pair is one move into that container, an unload with no
/// matching load is a take into the world, the loop's per-child pairs are ONE
/// container-expansion intent, and two targets inside one bracket are refused whole.
/// It is a type of its own because those pairing rules and their refusals are
/// self-contained; the window owns what a bracket is and what a captured call means,
/// this owns how the calls pair up.
/// </summary>
internal sealed class RemoteContainerMoveCapture
{
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
	private readonly List<PendingContainerCall> _calls = [];

	/// <summary>The children the container-expansion loop (R5) unloaded out of the dragged item's own container inside this bracket.</summary>
	private readonly HashSet<ulong> _batchChildren = [];

	private ulong _draggedItemId;
	private ulong _batchTargetContainerId;
	private bool _batchTargetConflict;
	private Action<string>? _refuse;

	/// <summary>True while this bracket has at least one container call to account for — the window's frame rule reads it.</summary>
	internal bool HasCalls => _calls.Count > 0 || _batchChildren.Count > 0;

	/// <summary>Start a bracket on the dragged proxy, with the window's refusal sink.</summary>
	internal void Reset(ulong draggedItemId, Action<string> refuse)
	{
		_calls.Clear();
		_batchChildren.Clear();
		_batchTargetContainerId = 0;
		_batchTargetConflict = false;
		_draggedItemId = draggedItemId;
		_refuse = refuse;
	}

	/// <summary>
	/// <c>Container.UnloadItem(item, null)</c> — half of the native move pair, the
	/// first half of one container-expansion iteration (R5), or a take into the
	/// world on its own.
	/// </summary>
	internal void Unload(ulong itemInstanceId, ulong containerInstanceId)
	{
		if (itemInstanceId == _draggedItemId)
		{
			_calls.Add(new PendingContainerCall(itemInstanceId, containerInstanceId, Pending: true));
			return;
		}

		// R5's per-child loop unloads each direct child out of the dragged item's
		// OWN container (the native source is `dragItem.container`,
		// PlayerCamera.cs:1589), so an unload whose source is the dragged item is the
		// batch's first half. The child identities are kept only to match the load
		// that follows: the intent carries the dragged item, and the owner enumerates
		// the children on the real objects.
		if (containerInstanceId == _draggedItemId)
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
	internal void Load(ulong itemInstanceId, ulong containerInstanceId)
	{
		if (itemInstanceId == _draggedItemId)
		{
			// The matching pending unload on THIS container makes the pair one move;
			// an unload still pending on another container stays pending and closes as
			// its own take-out, which is the native order of a release that takes an
			// item out of one container and drops it into another (W1 + W4).
			for (var index = _calls.Count - 1; index >= 0; index--)
			{
				var pending = _calls[index];
				if (pending.Pending && pending.ContainerInstanceId == containerInstanceId)
				{
					_calls[index] = pending with { Pending = false };
					return;
				}
			}

			_calls.Add(new PendingContainerCall(itemInstanceId, containerInstanceId, Pending: false));
			return;
		}

		// The second half of one R5 iteration: a child this bracket already saw
		// unloaded out of the dragged container now loads into the hit container.
		// Every child of that loop shares the target, so the first completed pair
		// names the ONE intent the gesture is and the remaining pairs are the same
		// gesture — one intent per child would make the owner run the loop once per
		// child on a container the first run already emptied.
		if (_batchChildren.Contains(itemInstanceId) && containerInstanceId != _draggedItemId)
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

	/// <summary>
	/// Append what this bracket's container calls became, in native call order: the
	/// expansion batch (its ONE intent, or the refusal when its children were unloaded
	/// with nothing loading them — a shape the native loop never produces), then each
	/// call — pending ones as a take into the world, paired ones as a move.
	/// </summary>
	internal void AppendIntents(List<RemoteDragIntent> intents)
	{
		if (_batchTargetConflict)
		{
			// The refusal already names both targets; a half-applied batch would be a
			// gesture the native loop never makes.
		}
		else if (_batchTargetContainerId != 0)
		{
			intents.Add(ContainerIntent(RemoteInventoryIntentKind.MoveContainerChildren, _draggedItemId, _batchTargetContainerId));
		}
		else if (_batchChildren.Count > 0)
		{
			Refuse($"container-expansion unload of {_batchChildren.Count} child item(s) with no load behind it — the native loop never leaves a child unloaded, so this gesture is not one the vocabulary can name");
		}

		foreach (var call in _calls)
		{
			intents.Add(call.Pending
				? ContainerIntent(RemoteInventoryIntentKind.TakeOutOfContainer, call.ItemInstanceId, call.ContainerInstanceId)
				: ContainerIntent(RemoteInventoryIntentKind.MoveIntoContainer, call.ItemInstanceId, call.ContainerInstanceId));
		}
	}

	private void Refuse(string what) => _refuse?.Invoke(what);

	/// <summary>A container intent carries the two ids and nothing else — no slot, no body, no limb.</summary>
	private static RemoteDragIntent ContainerIntent(RemoteInventoryIntentKind kind, ulong itemInstanceId, ulong containerInstanceId) =>
		new(kind, itemInstanceId, containerInstanceId, -1, 0, -1);

	/// <summary>One captured container call: pending until its `load` on the same container turns it into a move.</summary>
	private readonly record struct PendingContainerCall(ulong ItemInstanceId, ulong ContainerInstanceId, bool Pending);
}
