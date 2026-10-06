using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The drop-report pending machine (PURE — no Unity, no time source): an item
/// left its home into the world and the report waits for the operation to
/// settle. TWO hooks open a departure and each spans several calls — the body
/// drop (DropItem → ThrowItem / re-pick / container load / frame-end flush) and
/// the container unload (Container.UnloadItem → a container load in the same
/// bracket re-homes the item, otherwise it landed). ONE ENTRY PER ITEM: two
/// departures in one frame are TWO reports (a slot release onto an occupied
/// slot drops both occupants; a container expansion unloads one child per
/// refused load), never one overwriting the other.
/// <para>
/// All decisions are explicit inputs: the matching item id, the current frame
/// (a same-frame settle waits for the throw velocity), whether the pending item
/// is still alive and whether it is a standalone world item.
/// <see cref="Source"/> records where the item came from — the fact a container
/// load reads to classify the move it completes, because the scene cannot
/// answer it: the unload that opened the pair has already detached the item.
/// The GameAdapter's ItemDropState holds the game-side mapping (Item/position)
/// and feeds these inputs — this machine is what the tests lock.
/// </summary>
internal sealed class DropPendingState
{
	/// <summary>Where the item was before it left into the world.</summary>
	internal enum Source
	{
		/// <summary>The carried inventory: a body slot, a limb, or a body-side container.</summary>
		CarriedInventory,

		/// <summary>The world: a world container (a ground crate, a dropped bag).</summary>
		World,
	}

	/// <summary>One unsent departure report: the item, the frame it left in (the report waits that frame out) and the operation trace id it resolves with.</summary>
	internal readonly record struct Pending(ulong ItemId, int Frame, long Op, Source Source);

	private readonly Dictionary<ulong, Pending> _pending = [];

	internal bool HasPending => _pending.Count > 0;

	internal bool IsPendingFor(ulong itemId) => _pending.ContainsKey(itemId);

	/// <summary>
	/// The pending items, as a copy: resolving one's game facts (alive, a
	/// standalone world item) reads the scene and then SETTLES the entry, so the
	/// caller may not iterate the live set.
	/// </summary>
	internal List<ulong> CopyItemIds() => [.. _pending.Keys];

	/// <summary>
	/// Opens (or re-opens) one item's departure. A second departure of the SAME
	/// item replaces its own entry — the item cannot leave twice — while a
	/// departure of another item is held beside it.
	/// </summary>
	internal void EnterDrop(ulong itemId, int frame, long op, Source source) =>
		_pending[itemId] = new Pending(itemId, frame, op, source);

	/// <summary>
	/// Takes one item's pending departure — it resolved (a throw consumed it; a
	/// re-pick, a container load, a destroy or a replacing unload cancelled it).
	/// The CALLER decides what that means: the entry carries the op for the trace
	/// and <see cref="Pending.Source"/> for the container-load classification.
	/// </summary>
	internal bool TryTake(ulong itemId, out Pending pending)
	{
		if (!_pending.TryGetValue(itemId, out pending))
		{
			return false;
		}

		_pending.Remove(itemId);
		return true;
	}

	/// <summary>
	/// Settles one item's departure: the report is due when its frame has passed
	/// AND the item is alive AND it is a standalone world item. An entry that
	/// cannot report yet STAYS — a destroyed item or one still attached to a body
	/// (a drag-to-hand re-picked it within the departure frame) resolves through a
	/// later hook, and clearing it here swallowed the report forever ("the dropped
	/// flashlight never reported").
	/// </summary>
	internal bool TrySettle(ulong itemId, int currentFrame, bool alive, bool standalone, out Pending settled)
	{
		if (!_pending.TryGetValue(itemId, out var pending) || currentFrame <= pending.Frame || !alive || !standalone)
		{
			settled = default;
			return false;
		}

		_pending.Remove(itemId);
		settled = pending;
		return true;
	}

	/// <summary>
	/// The world was left (scene switch / session end) — every pending departure
	/// is gone with it. The entries come back so the trace stays balanced.
	/// </summary>
	internal void ResetAll(List<Pending> cancelled)
	{
		foreach (var pending in _pending.Values)
		{
			cancelled.Add(pending);
		}

		_pending.Clear();
	}
}
