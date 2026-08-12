using CasualtiesUnknownOnline.Runtime.Session.Items;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The drop-operation state (game side): one player drop spans several hooks
/// (DropItem → ThrowItem / re-pick / container load / frame-end flush) and the
/// pending state carries the operation across them. The transition DECISIONS
/// live in the pure <see cref="DropPendingState"/> machine (Runtime — testable,
/// version-independent); this shell holds the game-side mapping (the Item
/// reference, the drop position) and feeds the machine its explicit inputs
/// (item id, Time.frameCount, alive/standalone checks). Every transition goes
/// through this shell so the two states stay in sync.
/// </summary>
internal sealed class ItemDropState
{
	/// <summary>The lifecycle of one drop report.</summary>
	internal enum Phase
	{
		/// <summary>No drop in flight.</summary>
		Idle,

		/// <summary>A drop happened but the report waits — for the final throw velocity (ThrowItem lands a moment later) or the frame-end flush; a re-pick / container load / destruction cancels it.</summary>
		Dropped,
	}

	private readonly DropPendingState _machine = new();
	private (Item Item, Vector2 Pos)? _pending;

	internal Phase Current => _machine.HasPending ? Phase.Dropped : Phase.Idle;

	/// <summary>True when the pending drop belongs to this item.</summary>
	internal bool IsPendingFor(Item item) => _machine.IsPendingFor(ItemIdOf(item));

	/// <summary>Idle → Dropped. The caller flushes any prior pending drop of a DIFFERENT item first (two drops in one frame — rare).</summary>
	internal void EnterDrop(ulong itemId, Item item, Vector2 pos, long op)
	{
		_pending = (item, pos);
		_machine.EnterDrop(itemId, Time.frameCount, op);
	}

	/// <summary>Dropped → Idle: the drop resolved WITHOUT a report (re-picked into an
	/// inventory, loaded into a container, destroyed — another path reports the
	/// item's move, or it never happened). Returns the op id for the trace.</summary>
	internal bool TryCancel(Item item, out long op)
	{
		if (!_machine.TryCancel(ItemIdOf(item), out op))
		{
			return false;
		}

		_pending = null;
		return true;
	}

	/// <summary>Dropped → Idle: consumed by ThrowItem — report with the drop position and the op id.</summary>
	internal bool TryConsumeByThrow(Item item, out (Item Item, Vector2 Pos, long Op) dropped)
	{
		if (!_machine.TryConsumeByThrow(ItemIdOf(item), out var consumed))
		{
			dropped = default;
			return false;
		}

		var pending = _pending!.Value;
		_pending = null;
		dropped = (item, pending.Pos, consumed.Op);
		return true;
	}

	/// <summary>
	/// Dropped → Idle (frame-end flush) or stays Dropped. A same-frame flush
	/// (the throw velocity may still land), a destroyed item or an item still
	/// attached to the body (a drag-to-hand re-picked it within the drop frame —
	/// clearing here made that sequence swallow the pending drop forever, "the
	/// dropped flashlight never reported") keeps the pending state; those
	/// resolve through a later hook or the trace's begin-without-end assert.
	/// The machine's flush conditions are fed as explicit inputs.
	/// </summary>
	internal bool TryFlush(out (Item Item, Vector2 Pos, long Op) dropped)
	{
		if (_pending is not { } pending)
		{
			dropped = default;
			return false;
		}

		var alive = pending.Item != null; // Unity object — ==; destroyed while pending
		if (!_machine.TryFlush(Time.frameCount, alive, alive && ItemWorldSync.IsStandaloneWorldItem(pending.Item!), out var op))
		{
			dropped = default;
			return false;
		}

		_pending = null;
		dropped = (pending.Item!, pending.Pos, op);
		return true;
	}

	/// <summary>Dropped → Idle: the world was left (scene switch / session end) — the
	/// pending item is gone with it. Returns the op id so the trace stays balanced.</summary>
	internal bool TryReset(out long op)
	{
		if (!_machine.TryReset(out op))
		{
			return false;
		}

		_pending = null;
		return true;
	}

	private static ulong ItemIdOf(Item item) => item == null ? 0 : item.GetComponent<ItemInstanceId>()?.Id ?? 0;
}
